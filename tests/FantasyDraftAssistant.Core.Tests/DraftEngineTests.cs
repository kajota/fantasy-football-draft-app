using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class DraftEngineTests
{
    [Fact]
    public void Draft_player_commits_selection_and_advances()
    {
        var state = StartedState();
        var player = PlayerId.New();

        var result = DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, player));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, state.ActiveSelections.Count);
        Assert.True(state.UnavailablePlayers.Contains(player));
        Assert.Equal(2, state.CurrentOverallPick);
        Assert.Equal(2, state.Draft.CurrentStateVersion);
        Assert.Contains(result.Events, e => e.EventType == DraftEventType.PlayerDrafted);
    }

    [Fact]
    public void Cannot_draft_the_same_player_twice()
    {
        var state = StartedState();
        var player = PlayerId.New();
        Assert.True(DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, player)).Succeeded);
        var second = DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, player));
        Assert.False(second.Succeeded);
        Assert.Contains("not available", second.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rollback_restores_availability_and_supports_redo()
    {
        var state = StartedState();
        var a = PlayerId.New();
        var b = PlayerId.New();
        var c = PlayerId.New();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, a));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, b));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, c));

        var rollback = DraftEngine.Rollback(state, new RollbackDraftCommand(state.Draft.DraftId, 1));
        Assert.True(rollback.Succeeded, rollback.Error);
        Assert.Equal(1, state.ActiveSelections.Count);
        Assert.False(state.UnavailablePlayers.Contains(b));
        Assert.False(state.UnavailablePlayers.Contains(c));
        Assert.Equal(2, state.CurrentOverallPick);

        var redo = DraftEngine.Redo(state, new RedoDraftCommand(state.Draft.DraftId));
        Assert.True(redo.Succeeded, redo.Error);
        Assert.Equal(3, state.ActiveSelections.Count);
        Assert.True(state.UnavailablePlayers.Contains(c));
    }

    [Fact]
    public void Redo_is_invalidated_by_a_new_selection()
    {
        var state = StartedState();
        var a = PlayerId.New();
        var b = PlayerId.New();
        var replacement = PlayerId.New();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, a));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, b));
        DraftEngine.Rollback(state, new RollbackDraftCommand(state.Draft.DraftId, 1));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, replacement));

        var redo = DraftEngine.Redo(state, new RedoDraftCommand(state.Draft.DraftId));
        Assert.False(redo.Succeeded);
    }

    [Fact]
    public void Correct_pick_replaces_player_and_rewinds_later_picks()
    {
        var state = StartedState();
        var original = PlayerId.New();
        var later = PlayerId.New();
        var replacement = PlayerId.New();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, original));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, later));

        var result = DraftEngine.CorrectPick(state, new CorrectPickCommand(state.Draft.DraftId, 1, replacement));
        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(replacement, state.ActiveSelections[1].PlayerId);
        Assert.False(state.ActiveSelections.ContainsKey(2));
        Assert.False(state.UnavailablePlayers.Contains(original));
        Assert.False(state.UnavailablePlayers.Contains(later));
        Assert.Contains(result.Events, e => e.EventType == DraftEventType.PickCorrected);
    }

    [Fact]
    public void Branch_inherits_picks_before_branch_point()
    {
        var state = StartedState();
        var first = PlayerId.New();
        var second = PlayerId.New();
        var third = PlayerId.New();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, first));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, second));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, third));

        var result = DraftEngine.CreateBranch(state, new CreateDraftBranchCommand(state.Draft.DraftId, "WR at 1.03", 3));
        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(state.CreatedBranch);
        Assert.Equal(2, state.ActiveSelections.Count);
        Assert.True(state.ActiveSelections.ContainsKey(1));
        Assert.True(state.ActiveSelections.ContainsKey(2));
        Assert.False(state.ActiveSelections.ContainsKey(3));
        Assert.False(state.UnavailablePlayers.Contains(third));
        Assert.Equal(state.CreatedBranch!.BranchId, state.Draft.ActiveBranchId);
    }

    [Fact]
    public void Keeper_makes_player_unavailable_and_fills_slot()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 4);
        var keeperPlayer = PlayerId.New();
        var team = state.Teams[0];
        state.Keepers = [new Keeper
        {
            KeeperId = KeeperId.New(),
            DraftId = state.Draft.DraftId,
            TeamId = team.TeamId,
            PlayerId = keeperPlayer,
            RoundCost = 2
        }];

        var started = DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId));
        Assert.True(started.Succeeded, started.Error);
        Assert.True(state.UnavailablePlayers.Contains(keeperPlayer));
        var keeperSelection = state.ActiveSelections.Values.Single(s => s.Source == PickSource.Keeper);
        Assert.Equal(2, keeperSelection.Round);
        Assert.Equal(keeperPlayer, keeperSelection.PlayerId);
        Assert.Equal(1, state.CurrentOverallPick);
    }

    [Fact]
    public void ApplyKeepers_after_start_fills_empty_board()
    {
        var state = StartedState();
        var team = state.Teams[0];
        var keeperPlayer = PlayerId.New();
        state.Keepers =
        [
            new Keeper
            {
                KeeperId = KeeperId.New(),
                DraftId = state.Draft.DraftId,
                TeamId = team.TeamId,
                PlayerId = keeperPlayer,
                RoundCost = 2
            }
        ];

        var applied = DraftEngine.ApplyKeepers(state);
        Assert.True(applied.Succeeded, applied.Error);
        Assert.True(state.UnavailablePlayers.Contains(keeperPlayer));
        var selection = Assert.Single(state.ActiveSelections.Values);
        Assert.Equal(PickSource.Keeper, selection.Source);
        Assert.Equal(2, selection.Round);
        Assert.Equal(keeperPlayer, selection.PlayerId);
        Assert.Contains(applied.Events, e => e.EventType == DraftEventType.KeepersReplaced);
        Assert.Null(state.Redo);
    }

    [Fact]
    public void ApplyKeepers_rejects_when_regular_picks_exist()
    {
        var state = StartedState();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, PlayerId.New()));
        state.Keepers =
        [
            new Keeper
            {
                KeeperId = KeeperId.New(),
                DraftId = state.Draft.DraftId,
                TeamId = state.Teams[0].TeamId,
                PlayerId = PlayerId.New(),
                RoundCost = 2
            }
        ];

        var applied = DraftEngine.ApplyKeepers(state);
        Assert.False(applied.Succeeded);
        Assert.Contains("regular picks", applied.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ApplyKeepers_after_undoing_regular_picks_replaces_board()
    {
        var state = StartedState();
        var first = PlayerId.New();
        var second = PlayerId.New();
        var keeperPlayer = PlayerId.New();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, first));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, second));
        var undone = DraftEngine.Rollback(state, new RollbackDraftCommand(state.Draft.DraftId, 0));
        Assert.True(undone.Succeeded, undone.Error);
        Assert.Empty(state.ActiveSelections);
        Assert.NotNull(state.Redo);

        state.Keepers =
        [
            new Keeper
            {
                KeeperId = KeeperId.New(),
                DraftId = state.Draft.DraftId,
                TeamId = state.Teams[0].TeamId,
                PlayerId = keeperPlayer,
                RoundCost = 3
            }
        ];

        var applied = DraftEngine.ApplyKeepers(state);
        Assert.True(applied.Succeeded, applied.Error);
        var selection = Assert.Single(state.ActiveSelections.Values);
        Assert.Equal(PickSource.Keeper, selection.Source);
        Assert.Equal(3, selection.Round);
        Assert.Equal(keeperPlayer, selection.PlayerId);
        Assert.Null(state.Redo);

        var events = state.NewEvents.ToList();
        state.ActiveSelections.Clear();
        state.UnavailablePlayers.Clear();
        state.Redo = null;
        DraftEngine.RebuildActiveTimeline(state, events);

        var rebuilt = Assert.Single(state.ActiveSelections.Values);
        Assert.Equal(keeperPlayer, rebuilt.PlayerId);
        Assert.Equal(PickSource.Keeper, rebuilt.Source);
        Assert.Null(state.Redo);
    }

    [Fact]
    public void One_keeper_per_team_is_enforced()
    {
        var team = TeamId.New();
        var result = KeeperRules.Validate(
        [
            new KeeperSpec { TeamId = team, PlayerId = PlayerId.New(), RoundCost = 1 },
            new KeeperSpec { TeamId = team, PlayerId = PlayerId.New(), RoundCost = 2 }
        ]);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Superflex_qb_demand_counts_flex_slots()
    {
        var state = LeagueFactory.CreateStandardState(superflex: true);
        Assert.Equal(2, RosterRules.QbDemand(state.RosterSlots));
        Assert.True(RosterRules.IsSuperflexOrMultiQb(state.RosterSlots));
    }

    [Fact]
    public void Rebuild_from_events_matches_live_state()
    {
        var state = StartedState();
        var a = PlayerId.New();
        var b = PlayerId.New();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, a));
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, b));
        DraftEngine.Rollback(state, new RollbackDraftCommand(state.Draft.DraftId, 1));

        var events = state.NewEvents.ToList();
        state.ActiveSelections.Clear();
        state.UnavailablePlayers.Clear();
        state.Redo = null;
        DraftEngine.RebuildActiveTimeline(state, events);

        Assert.Single(state.ActiveSelections);
        Assert.Equal(a, state.ActiveSelections[1].PlayerId);
        Assert.False(state.ActiveSelections.ContainsKey(2));
        Assert.NotNull(state.Redo);
    }

    [Fact]
    public void Integrity_checker_accepts_consistent_state()
    {
        var state = StartedState();
        DraftEngine.DraftPlayer(state, new DraftPlayerCommand(state.Draft.DraftId, PlayerId.New()));
        var report = IntegrityChecker.Validate(state);
        Assert.True(report.IsHealthy, string.Join("; ", report.Issues));
    }

    private static FantasyDraftAssistant.Core.Results.DraftWorkingState StartedState()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 4);
        var start = DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId));
        Assert.True(start.Succeeded, start.Error);
        return state;
    }
}
