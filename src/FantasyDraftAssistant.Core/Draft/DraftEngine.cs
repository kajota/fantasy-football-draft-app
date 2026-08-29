using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Core.Serialization;

namespace FantasyDraftAssistant.Core.Engine;

public static class DraftEngine
{
    public static DraftCommitResult StartDraft(DraftWorkingState state, StartDraftCommand command)
    {
        var validation = DraftValidator.CanStart(state);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var startedAt = DateTimeOffset.UtcNow;
        state.Draft.Status = DraftStatus.InProgress;
        state.Draft.StartedAt = startedAt;
        IncrementVersion(state);

        AppendEvent(state, DraftEventType.DraftStarted, command.CreatedBy, new DraftStartedPayload
        {
            LeagueId = state.League.LeagueId.Value,
            MainBranchId = state.ActiveBranch.BranchId.Value
        });

        foreach (var keeper in state.Keepers)
        {
            var slot = KeeperRules.ResolveSlot(state.Slots, keeper)
                       ?? throw new InvalidOperationException("Keeper slot vanished after validation.");
            slot.IsKeeperSlot = true;
            ApplySelection(state, slot, keeper.PlayerId, PickSource.Keeper, null, startedAt, command.CreatedBy);
        }

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult ApplyKeepers(DraftWorkingState state, string createdBy = "user")
    {
        if (state.Draft.Status == DraftStatus.NotStarted)
            return DraftCommitResult.Ok(state, state.NewEvents);

        var editCheck = KeeperRules.ValidateCanEditKeepers(state.Draft.Status, state.ActiveSelections.Values);
        if (!editCheck.IsValid)
            return DraftCommitResult.Fail(editCheck.Error!);

        var keeperSpecs = state.Keepers.Select(keeper => new KeeperSpec
        {
            TeamId = keeper.TeamId,
            PlayerId = keeper.PlayerId,
            RoundCost = keeper.RoundCost,
            Notes = keeper.Notes
        }).ToList();
        var keeperValidation = KeeperRules.Validate(keeperSpecs);
        if (!keeperValidation.IsValid)
            return DraftCommitResult.Fail(keeperValidation.Error!);

        var resolved = new List<(Keeper Keeper, DraftSlot Slot)>();
        foreach (var keeper in state.Keepers)
        {
            var slot = KeeperRules.ResolveSlot(state.Slots, keeper);
            if (slot is null)
                return DraftCommitResult.Fail($"Keeper for team {keeper.TeamId} has no matching draft slot in round {keeper.RoundCost}.");
            resolved.Add((keeper, slot));
        }

        IncrementVersion(state);
        var removed = state.ActiveSelections.Values
            .Where(selection => selection.Source == PickSource.Keeper)
            .OrderBy(selection => selection.OverallPick)
            .ToList();
        foreach (var selection in removed)
            Deactivate(state, selection.OverallPick);

        foreach (var slot in state.Slots)
            slot.IsKeeperSlot = false;

        AppendEvent(state, DraftEventType.KeepersReplaced, createdBy, new KeepersReplacedPayload
        {
            DeactivatedOverallPicks = removed.Select(selection => selection.OverallPick).ToArray()
        });

        var appliedAt = DateTimeOffset.UtcNow;
        foreach (var (keeper, slot) in resolved)
        {
            slot.IsKeeperSlot = true;
            ApplySelection(state, slot, keeper.PlayerId, PickSource.Keeper, null, appliedAt, createdBy);
        }

        state.Redo = null;
        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult DraftPlayer(DraftWorkingState state, DraftPlayerCommand command)
    {
        var validation = DraftValidator.CanDraftPlayer(state, command.PlayerId);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var slot = state.CurrentSlot!;
        IncrementVersion(state);
        ApplySelection(
            state,
            slot,
            command.PlayerId,
            command.Source,
            command.ExternalSourceId,
            command.ObservedAt ?? DateTimeOffset.UtcNow,
            command.CreatedBy);
        state.Redo = null;

        if (state.CurrentSlot is null)
            state.Draft.Status = DraftStatus.Completed;

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult Rollback(DraftWorkingState state, RollbackDraftCommand command)
    {
        var validation = DraftValidator.CanRollback(state, command.TargetOverallPick);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var deactivated = state.ActiveSelections.Values
            .Where(s => s.OverallPick > command.TargetOverallPick && s.Source != PickSource.Keeper)
            .OrderBy(s => s.OverallPick)
            .ToList();

        IncrementVersion(state);
        foreach (var selection in deactivated)
            Deactivate(state, selection.OverallPick);

        state.Redo = new RedoCandidate
        {
            Selections = deactivated,
            RolledBackToOverallPick = command.TargetOverallPick
        };
        state.Draft.Status = DraftStatus.InProgress;
        state.Draft.CompletedAt = null;

        AppendEvent(state, DraftEventType.DraftRolledBack, command.CreatedBy, new DraftRolledBackPayload
        {
            TargetOverallPick = command.TargetOverallPick,
            DeactivatedOverallPicks = deactivated.Select(s => s.OverallPick).ToArray()
        });

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    /// <summary>
    /// Clears the board back to the first pick, keeping keepers. Recorded as a single rollback
    /// so it is one Undo away, rather than the pick-by-pick unwinding it replaces.
    /// </summary>
    public static DraftCommitResult Reset(DraftWorkingState state, ResetDraftCommand command)
    {
        var validation = DraftValidator.CanReset(state);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var deactivated = state.ActiveSelections.Values
            .Where(s => s.Source != PickSource.Keeper)
            .OrderBy(s => s.OverallPick)
            .ToList();

        IncrementVersion(state);
        foreach (var selection in deactivated)
            Deactivate(state, selection.OverallPick);

        state.Redo = new RedoCandidate
        {
            Selections = deactivated,
            RolledBackToOverallPick = 0
        };
        state.Draft.Status = DraftStatus.InProgress;
        state.Draft.CompletedAt = null;

        AppendEvent(state, DraftEventType.DraftRolledBack, command.CreatedBy, new DraftRolledBackPayload
        {
            TargetOverallPick = 0,
            DeactivatedOverallPicks = deactivated.Select(s => s.OverallPick).ToArray()
        });

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult Redo(DraftWorkingState state, RedoDraftCommand command)
    {
        var validation = DraftValidator.CanRedo(state);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var redo = state.Redo!;
        IncrementVersion(state);

        foreach (var selection in redo.Selections.OrderBy(s => s.OverallPick))
        {
            var slot = state.Slots.First(s => s.OverallPick == selection.OverallPick);
            ApplySelection(
                state,
                slot,
                selection.PlayerId,
                selection.Source,
                selection.ExternalSourceId,
                selection.ObservedAt,
                command.CreatedBy);
        }

        AppendEvent(state, DraftEventType.DraftRedone, command.CreatedBy, new DraftRedonePayload
        {
            RestoredOverallPicks = redo.Selections.Select(s => s.OverallPick).ToArray()
        });

        state.Redo = null;
        if (state.CurrentSlot is null)
            state.Draft.Status = DraftStatus.Completed;

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult CorrectPick(DraftWorkingState state, CorrectPickCommand command)
    {
        var validation = DraftValidator.CanCorrect(state, command.OverallPick, command.ReplacementPlayerId);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var original = state.ActiveSelections[command.OverallPick];
        var rollback = Rollback(state, new RollbackDraftCommand(state.Draft.DraftId, command.OverallPick - 1, command.CreatedBy));
        if (!rollback.Succeeded)
            return rollback;

        AppendEvent(state, DraftEventType.PickCorrected, command.CreatedBy, new PickCorrectedPayload
        {
            OverallPick = command.OverallPick,
            OriginalPlayerId = original.PlayerId.Value,
            ReplacementPlayerId = command.ReplacementPlayerId.Value,
            OriginalEventId = original.EventId.Value
        });

        var draft = DraftPlayer(state, new DraftPlayerCommand(
            state.Draft.DraftId,
            command.ReplacementPlayerId,
            original.Source,
            original.ExternalSourceId,
            DateTimeOffset.UtcNow,
            command.CreatedBy));
        if (!draft.Succeeded)
            return draft;

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult CreateBranch(DraftWorkingState state, CreateDraftBranchCommand command)
    {
        var validation = DraftValidator.CanCreateBranch(state, command.BranchPointOverallPick, command.Name);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        var newBranch = new DraftBranch
        {
            BranchId = BranchId.New(),
            DraftId = state.Draft.DraftId,
            Name = command.Name.Trim(),
            ParentBranchId = state.ActiveBranch.BranchId,
            BranchPointOverallPick = command.BranchPointOverallPick,
            CreatedFromStateVersion = state.Draft.CurrentStateVersion,
            CurrentHeadEventId = state.ActiveBranch.CurrentHeadEventId
        };

        IncrementVersion(state);
        AppendEvent(state, DraftEventType.DraftBranchCreated, command.CreatedBy, new DraftBranchCreatedPayload
        {
            NewBranchId = newBranch.BranchId.Value,
            Name = newBranch.Name,
            ParentBranchId = state.ActiveBranch.BranchId.Value,
            BranchPointOverallPick = command.BranchPointOverallPick
        });

        state.CreatedBranch = newBranch;
        if (command.SwitchToNewBranch)
            SwitchToBranch(state, newBranch, command.BranchPointOverallPick);

        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult SwitchBranch(DraftWorkingState state, DraftBranch branch)
    {
        state.Draft.ActiveBranchId = branch.BranchId;
        state.ActiveBranch = branch;
        state.Redo = null;
        if (state.CurrentSlot is null)
        {
            state.Draft.Status = DraftStatus.Completed;
            state.Draft.CompletedAt ??= DateTimeOffset.UtcNow;
        }
        else
        {
            state.Draft.Status = DraftStatus.InProgress;
            state.Draft.CompletedAt = null;
        }

        IncrementVersion(state);
        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static DraftCommitResult Complete(DraftWorkingState state, CompleteDraftCommand command)
    {
        var validation = DraftValidator.CanComplete(state);
        if (!validation.IsValid)
            return DraftCommitResult.Fail(validation.Error!);

        IncrementVersion(state);
        state.Draft.Status = DraftStatus.Completed;
        state.Draft.CompletedAt = DateTimeOffset.UtcNow;
        AppendEvent(state, DraftEventType.DraftCompleted, command.CreatedBy, new DraftCompletedPayload
        {
            CompletedAt = state.Draft.CompletedAt.Value
        });
        return DraftCommitResult.Ok(state, state.NewEvents);
    }

    public static void RebuildActiveTimeline(DraftWorkingState state, IReadOnlyList<DraftEventRecord> events)
    {
        state.ActiveSelections.Clear();
        state.UnavailablePlayers.Clear();
        state.Redo = null;

        foreach (var keeper in state.Keepers)
            state.UnavailablePlayers.Add(keeper.PlayerId);

        foreach (var evt in events.OrderBy(e => e.SequenceNumber))
        {
            switch (evt.EventType)
            {
                case DraftEventType.PlayerDrafted:
                    var drafted = DraftJson.Deserialize<PlayerDraftedPayload>(evt.PayloadJson);
                    state.ActiveSelections[drafted.OverallPick] = new ActiveSelection
                    {
                        EventId = evt.EventId,
                        DraftId = evt.DraftId,
                        BranchId = evt.BranchId,
                        DraftSlotId = new DraftSlotId(drafted.DraftSlotId),
                        OverallPick = drafted.OverallPick,
                        Round = drafted.Round,
                        RoundPick = drafted.RoundPick,
                        TeamId = new TeamId(drafted.TeamId),
                        PlayerId = new PlayerId(drafted.PlayerId),
                        Source = drafted.Source,
                        ExternalSourceId = drafted.ExternalSourceId,
                        ObservedAt = drafted.ObservedAt
                    };
                    state.UnavailablePlayers.Add(new PlayerId(drafted.PlayerId));
                    break;
                case DraftEventType.DraftRolledBack:
                    var rolled = DraftJson.Deserialize<DraftRolledBackPayload>(evt.PayloadJson);
                    var removed = new List<ActiveSelection>();
                    foreach (var pick in rolled.DeactivatedOverallPicks)
                    {
                        if (state.ActiveSelections.Remove(pick, out var selection))
                        {
                            state.UnavailablePlayers.Remove(selection.PlayerId);
                            removed.Add(selection);
                        }
                    }

                    state.Redo = new RedoCandidate
                    {
                        Selections = removed,
                        RolledBackToOverallPick = rolled.TargetOverallPick
                    };
                    break;
                case DraftEventType.DraftRedone:
                    state.Redo = null;
                    break;
                case DraftEventType.KeepersReplaced:
                    var replaced = DraftJson.Deserialize<KeepersReplacedPayload>(evt.PayloadJson);
                    foreach (var pick in replaced.DeactivatedOverallPicks)
                    {
                        if (state.ActiveSelections.Remove(pick, out var removedKeeper))
                            state.UnavailablePlayers.Remove(removedKeeper.PlayerId);
                    }

                    state.Redo = null;
                    break;
            }
        }

        foreach (var keeper in state.Keepers)
            state.UnavailablePlayers.Add(keeper.PlayerId);
    }

    private static void SwitchToBranch(DraftWorkingState state, DraftBranch branch, int branchPointOverallPick)
    {
        var inherited = state.ActiveSelections.Values
            .Where(s => s.OverallPick < branchPointOverallPick || s.Source == PickSource.Keeper)
            .ToList();

        state.ActiveSelections.Clear();
        state.UnavailablePlayers.Clear();
        foreach (var keeper in state.Keepers)
            state.UnavailablePlayers.Add(keeper.PlayerId);

        foreach (var selection in inherited)
        {
            state.ActiveSelections[selection.OverallPick] = selection;
            state.UnavailablePlayers.Add(selection.PlayerId);
        }

        state.ActiveBranch = branch;
        state.Draft.ActiveBranchId = branch.BranchId;
        state.Draft.Status = DraftStatus.InProgress;
        state.Draft.CompletedAt = null;
        state.Redo = null;
        state.Queue.RemoveAll(q => state.UnavailablePlayers.Contains(q.PlayerId));
    }

    private static void ApplySelection(
        DraftWorkingState state,
        DraftSlot slot,
        PlayerId playerId,
        PickSource source,
        string? externalSourceId,
        DateTimeOffset observedAt,
        string createdBy)
    {
        var selection = new ActiveSelection
        {
            EventId = EventId.New(),
            DraftId = state.Draft.DraftId,
            BranchId = state.ActiveBranch.BranchId,
            DraftSlotId = slot.DraftSlotId,
            OverallPick = slot.OverallPick,
            Round = slot.Round,
            RoundPick = slot.RoundPick,
            TeamId = slot.TeamId,
            PlayerId = playerId,
            Source = source,
            ExternalSourceId = externalSourceId,
            ObservedAt = observedAt
        };

        state.ActiveSelections[slot.OverallPick] = selection;
        state.UnavailablePlayers.Add(playerId);
        state.Queue.RemoveAll(q => q.PlayerId.Equals(playerId));

        var evt = AppendEvent(state, DraftEventType.PlayerDrafted, createdBy, new PlayerDraftedPayload
        {
            DraftSlotId = slot.DraftSlotId.Value,
            OverallPick = slot.OverallPick,
            Round = slot.Round,
            RoundPick = slot.RoundPick,
            TeamId = slot.TeamId.Value,
            PlayerId = playerId.Value,
            Source = source,
            ExternalSourceId = externalSourceId,
            ObservedAt = observedAt
        }, selection.EventId);

        state.ActiveBranch.CurrentHeadEventId = evt.EventId;
    }

    private static void Deactivate(DraftWorkingState state, int overallPick)
    {
        if (!state.ActiveSelections.Remove(overallPick, out var selection))
            return;

        state.UnavailablePlayers.Remove(selection.PlayerId);
        foreach (var keeper in state.Keepers.Where(k => k.PlayerId.Equals(selection.PlayerId)))
        {
            var slot = KeeperRules.ResolveSlot(state.Slots, keeper);
            if (slot is not null && slot.OverallPick <= overallPick)
                state.UnavailablePlayers.Add(keeper.PlayerId);
        }
    }

    private static void IncrementVersion(DraftWorkingState state) =>
        state.Draft.CurrentStateVersion++;

    private static DraftEventRecord AppendEvent<T>(
        DraftWorkingState state,
        DraftEventType type,
        string createdBy,
        T payload,
        EventId? eventId = null)
    {
        var record = new DraftEventRecord
        {
            EventId = eventId ?? EventId.New(),
            DraftId = state.Draft.DraftId,
            BranchId = state.ActiveBranch.BranchId,
            EventType = type,
            StateVersion = state.Draft.CurrentStateVersion,
            SequenceNumber = state.NextSequenceNumber++,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = createdBy,
            CorrelationId = null,
            PayloadJson = DraftJson.Serialize(payload)
        };
        state.NewEvents.Add(record);
        state.ActiveBranch.CurrentHeadEventId = record.EventId;
        return record;
    }
}
