using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Services;

internal static class DraftStateLoader
{
    public static DraftWorkingState Load(SqliteConnection db, SqliteTransaction? tx, DraftId draftId, BranchId? branchId = null)
    {
        var draft = LeagueService.LoadDraft(db, tx, draftId)
                    ?? throw new InvalidOperationException("Draft not found.");
        var league = LeagueService.LoadLeague(db, tx, draft.LeagueId)
                     ?? throw new InvalidOperationException("League not found.");
        var requested = branchId ?? draft.ActiveBranchId;
        var branch = LeagueService.LoadBranch(db, tx, requested);
        if (branch is null || !branch.DraftId.Equals(draftId))
            branch = LeagueService.LoadBranch(db, tx, draft.ActiveBranchId);
        if (branch is null)
            throw new InvalidOperationException("Draft branch not found.");
        var activeBranchId = branch.BranchId;

        var state = new DraftWorkingState
        {
            League = league,
            Draft = draft,
            ActiveBranch = branch,
            Teams = LeagueService.LoadTeams(db, tx, league.LeagueId),
            RosterSlots = LeagueService.LoadRoster(db, tx, league.LeagueId),
            ScoringRules = LeagueService.LoadScoring(db, tx, league.LeagueId),
            Slots = LeagueService.LoadSlots(db, tx, draftId),
            Keepers = LeagueService.LoadKeepers(db, tx, draftId),
            NextSequenceNumber = LeagueService.NextSequence(db, tx, draftId)
        };

        foreach (var player in LeagueService.LoadPlayers(db, tx))
            state.KnownPlayers.Add(player.PlayerId);

        var selections = LeagueService.LoadSelections(db, tx, draftId, activeBranchId);
        if (selections.Count == 0 && branch.ParentBranchId is { } parentId)
        {
            selections = LeagueService.LoadSelections(db, tx, draftId, parentId)
                .Where(s => s.OverallPick < branch.BranchPointOverallPick || s.Source == PickSource.Keeper)
                .ToList();
        }

        if (selections.Count == 0)
        {
            var events = LeagueService.LoadEvents(db, tx, draftId, activeBranchId);
            if (events.Count > 0)
                DraftEngine.RebuildActiveTimeline(state, events);
        }
        else
        {
            foreach (var selection in selections)
            {
                state.ActiveSelections[selection.OverallPick] = selection;
                state.UnavailablePlayers.Add(selection.PlayerId);
            }

            foreach (var keeper in state.Keepers)
                state.UnavailablePlayers.Add(keeper.PlayerId);
        }

        state.Queue.AddRange(LeagueService.LoadQueue(db, tx, draftId, activeBranchId));
        return state;
    }

    public static DraftWorkingState LoadWithoutRequiredTx(SqliteConnection db, DraftId draftId, BranchId? branchId = null)
    {
        using var tx = db.BeginTransaction();
        var state = Load(db, tx, draftId, branchId);
        tx.Commit();
        return state;
    }
}
