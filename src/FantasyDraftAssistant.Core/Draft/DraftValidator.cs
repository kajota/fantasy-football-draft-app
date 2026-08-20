using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

public static class DraftValidator
{
    public static ValidationResult CanStart(DraftWorkingState state)
    {
        if (state.Draft.Status == DraftStatus.InProgress)
            return ValidationResult.Fail("Draft has already started.");
        if (state.Draft.Status == DraftStatus.Completed)
            return ValidationResult.Fail("Draft is already completed.");
        if (state.Slots.Count == 0)
            return ValidationResult.Fail("Draft order has not been generated.");
        if (state.Teams.Count == 0)
            return ValidationResult.Fail("League has no teams.");
        if (state.League.UserTeamId is null)
            return ValidationResult.Fail("The user's team has not been selected.");

        var keeperValidation = KeeperRules.Validate(
            state.Keepers.Select(k => new KeeperSpec
            {
                TeamId = k.TeamId,
                PlayerId = k.PlayerId,
                RoundCost = k.RoundCost,
                Notes = k.Notes
            }).ToList());
        if (!keeperValidation.IsValid)
            return keeperValidation;

        foreach (var keeper in state.Keepers)
        {
            if (KeeperRules.ResolveSlot(state.Slots, keeper) is null)
                return ValidationResult.Fail($"Keeper for team {keeper.TeamId} has no matching draft slot in round {keeper.RoundCost}.");
        }

        return ValidationResult.Ok();
    }

    public static ValidationResult CanDraftPlayer(DraftWorkingState state, PlayerId playerId)
    {
        if (state.Draft.Status != DraftStatus.InProgress)
            return ValidationResult.Fail("Draft is not in progress.");

        var slot = state.CurrentSlot;
        if (slot is null)
            return ValidationResult.Fail("There is no open draft slot. The draft is complete.");

        if (state.ActiveSelections.ContainsKey(slot.OverallPick))
            return ValidationResult.Fail($"Slot {DraftSlotGenerator.FormatRoundPick(slot.Round, slot.RoundPick)} already has an active selection.");

        if (state.UnavailablePlayers.Contains(playerId))
            return ValidationResult.Fail("That player is not available.");
        if (state.KnownPlayers.Count > 0 && !state.KnownPlayers.Contains(playerId))
            return ValidationResult.Fail("That player is not in the local player database.");

        return ValidationResult.Ok();
    }

    public static ValidationResult CanReset(DraftWorkingState state)
    {
        if (!state.ActiveSelections.Values.Any(s => s.Source != PickSource.Keeper))
            return ValidationResult.Fail("This board has no picks to clear.");

        return ValidationResult.Ok();
    }

    public static ValidationResult CanRollback(DraftWorkingState state, int targetOverallPick)
    {
        if (state.Draft.Status != DraftStatus.InProgress)
            return ValidationResult.Fail("Draft is not in progress.");
        if (targetOverallPick < 0)
            return ValidationResult.Fail("Rollback target is invalid.");

        var maxActive = state.ActiveSelections.Count == 0 ? 0 : state.ActiveSelections.Keys.Max();
        if (targetOverallPick > maxActive)
            return ValidationResult.Fail("Rollback target is after the last active selection.");

        if (targetOverallPick > 0 && !state.ActiveSelections.ContainsKey(targetOverallPick))
            return ValidationResult.Fail("Rollback target is not an active selection.");

        var deactivated = state.ActiveSelections.Keys.Where(p => p > targetOverallPick).ToList();
        if (deactivated.Count == 0)
            return ValidationResult.Fail("Nothing to roll back after the selected pick.");

        return ValidationResult.Ok();
    }

    public static ValidationResult CanRedo(DraftWorkingState state)
    {
        if (state.Redo is null || state.Redo.Selections.Count == 0)
            return ValidationResult.Fail("There is nothing to redo.");

        foreach (var selection in state.Redo.Selections.OrderBy(s => s.OverallPick))
        {
            if (state.ActiveSelections.ContainsKey(selection.OverallPick))
                return ValidationResult.Fail("Redo is no longer valid because a newer selection occupies a restored slot.");
            if (state.UnavailablePlayers.Contains(selection.PlayerId))
                return ValidationResult.Fail("Redo is no longer valid because a restored player is no longer available.");
        }

        return ValidationResult.Ok();
    }

    public static ValidationResult CanCorrect(DraftWorkingState state, int overallPick, PlayerId replacementPlayerId)
    {
        if (!state.ActiveSelections.TryGetValue(overallPick, out var original))
            return ValidationResult.Fail("There is no active selection at that pick.");
        if (original.PlayerId.Equals(replacementPlayerId))
            return ValidationResult.Fail("Replacement player is the same as the original selection.");
        if (state.UnavailablePlayers.Contains(replacementPlayerId) &&
            !state.ActiveSelections.Values.Any(s => s.OverallPick > overallPick && s.PlayerId.Equals(replacementPlayerId)))
        {
            if (!original.PlayerId.Equals(replacementPlayerId))
            {
                var stillTaken = state.ActiveSelections.Values.Any(s =>
                    s.OverallPick != overallPick && s.PlayerId.Equals(replacementPlayerId));
                if (stillTaken)
                    return ValidationResult.Fail("Replacement player is not available.");
            }
        }

        return ValidationResult.Ok();
    }

    public static ValidationResult CanCreateBranch(DraftWorkingState state, int branchPointOverallPick, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ValidationResult.Fail("Branch name is required.");
        if (branchPointOverallPick < 1)
            return ValidationResult.Fail("Branch point must be an existing pick.");
        if (!state.ActiveSelections.ContainsKey(branchPointOverallPick) &&
            !state.Slots.Any(s => s.OverallPick == branchPointOverallPick))
        {
            return ValidationResult.Fail("Branch point does not exist.");
        }

        return ValidationResult.Ok();
    }

    public static ValidationResult CanComplete(DraftWorkingState state)
    {
        if (state.Draft.Status != DraftStatus.InProgress)
            return ValidationResult.Fail("Draft is not in progress.");
        if (state.CurrentSlot is not null)
            return ValidationResult.Fail("The draft still has open slots.");
        return ValidationResult.Ok();
    }
}
