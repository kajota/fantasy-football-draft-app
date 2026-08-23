using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Tests;

public class BoardSnapshotBuilderTests
{
    [Fact]
    public void Marks_practice_and_on_the_clock()
    {
        var leagueId = LeagueId.New();
        var draftId = DraftId.New();
        var live = new DraftBranch
        {
            BranchId = BranchId.New(),
            DraftId = draftId,
            Name = "Live"
        };
        var practice = new DraftBranch
        {
            BranchId = BranchId.New(),
            DraftId = draftId,
            Name = "Practice",
            ParentBranchId = live.BranchId
        };
        var mine = Team("Mine", 1, leagueId);
        var other = Team("Other", 2, leagueId);
        var slots = new[]
        {
            Slot(draftId, 1, 1, 1, mine),
            Slot(draftId, 2, 1, 2, other)
        };
        var bijan = new Player
        {
            PlayerId = PlayerId.FromName("Bijan Robinson", "RB"),
            Name = "Bijan Robinson",
            NflTeam = "ATL",
            PrimaryPosition = PlayerPosition.RB,
            EligiblePositions = [PlayerPosition.RB]
        };
        var state = new DraftWorkingState
        {
            League = new League
            {
                LeagueId = leagueId,
                Name = "FilthyMothers",
                UserTeamId = mine.TeamId,
                Season = 2026,
                TeamCount = 2,
                RoundCount = 1,
                RosterSize = 1
            },
            Draft = new Draft
            {
                DraftId = draftId,
                LeagueId = leagueId,
                Name = "Test",
                ActiveBranchId = practice.BranchId
            },
            ActiveBranch = practice,
            Teams = [mine, other],
            RosterSlots = [],
            ScoringRules = [],
            Slots = slots,
            Keepers = []
        };
        state.ActiveSelections[1] = new ActiveSelection
        {
            EventId = EventId.New(),
            DraftId = draftId,
            BranchId = practice.BranchId,
            DraftSlotId = slots[0].DraftSlotId,
            OverallPick = 1,
            Round = 1,
            RoundPick = 1,
            TeamId = mine.TeamId,
            PlayerId = bijan.PlayerId
        };

        var snapshot = BoardSnapshotBuilder.Build(state, [bijan], DateTimeOffset.Parse("2026-08-23T12:00:00Z"));

        Assert.True(snapshot.Practice);
        Assert.Equal("FilthyMothers", snapshot.League);
        Assert.Equal("1.02", snapshot.Pick);
        Assert.Equal("Other", snapshot.OnTheClock?.Team);
        Assert.False(snapshot.OnTheClock?.IsMine);
        Assert.Equal("Bijan Robinson", snapshot.LastPick?.Player);
        Assert.Equal("RB", snapshot.LastPick?.Position);
        Assert.Equal("Bijan Robinson", snapshot.Rounds[0].Cells[0].Player);
        Assert.True(snapshot.Rounds[0].Cells[1].IsCurrent);
        Assert.True(snapshot.Rounds[0].Cells[1].IsEmpty);
        Assert.Equal("", snapshot.Rounds[0].Cells[1].Player);
    }

    private static Team Team(string name, int position, LeagueId leagueId) => new()
    {
        TeamId = TeamId.New(),
        LeagueId = leagueId,
        Name = name,
        DraftPosition = position
    };

    private static DraftSlot Slot(DraftId draftId, int overall, int round, int roundPick, Team team) => new()
    {
        DraftSlotId = DraftSlotId.New(),
        DraftId = draftId,
        OverallPick = overall,
        Round = round,
        RoundPick = roundPick,
        TeamId = team.TeamId
    };
}
