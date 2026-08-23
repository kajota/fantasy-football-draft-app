using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class DraftGridBuilderTests
{
    [Fact]
    public void Snake_keeps_team_columns_stable()
    {
        var a = Team("Matt", 1);
        var b = Team("Mike", 2);
        var slots = new[]
        {
            Slot(1, 1, 1, a),
            Slot(2, 1, 2, b),
            Slot(3, 2, 1, b),
            Slot(4, 2, 2, a)
        };
        var picks = new Dictionary<int, DraftGridPick>
        {
            [1] = new(1, a.TeamId, 1, "Jeanty", "RB"),
            [3] = new(3, b.TeamId, 2, "Chase", "WR")
        };

        var grid = DraftGridBuilder.Build(teams: [a, b], slots, picks, currentOverallPick: 2, userTeamId: a.TeamId);

        Assert.Equal(["Matt", "Mike"], grid.Teams.Select(t => t.Label));
        Assert.True(grid.Teams[0].IsMine);
        Assert.False(grid.Teams[1].IsMine);
        Assert.Equal("Jeanty", grid.Rounds[0].Cells[0].Player);
        Assert.Equal("On the clock", grid.Rounds[0].Cells[1].Player);
        Assert.True(grid.Rounds[0].Cells[1].IsCurrent);
        Assert.True(grid.Rounds[1].Cells[0].IsEmpty);
        Assert.Equal("Chase", grid.Rounds[1].Cells[1].Player);
        Assert.Equal("WR", grid.Rounds[1].Cells[1].Position);
    }

    [Fact]
    public void Filled_cell_gets_adp_heat()
    {
        var a = Team("Matt", 1);
        var b = Team("Mike", 2);
        var slots = new[]
        {
            Slot(1, 1, 1, a),
            Slot(2, 1, 2, b)
        };
        var picks = new Dictionary<int, DraftGridPick>
        {
            [1] = new(1, a.TeamId, 1, "Jeanty", "RB", Adp: 40)
        };
        var grid = DraftGridBuilder.Build([a, b], slots, picks, currentOverallPick: 2, userTeamId: a.TeamId);
        Assert.Equal(PickHeat.Reach, grid.Rounds[0].Cells[0].Heat);
        Assert.Equal(PickHeat.Empty, grid.Rounds[0].Cells[1].Heat);
    }

    private static Team Team(string name, int position) => new()
    {
        TeamId = new TeamId(Guid.NewGuid()),
        LeagueId = new LeagueId(Guid.NewGuid()),
        Name = name,
        DraftPosition = position
    };

    private static DraftSlot Slot(int overall, int round, int roundPick, Team team) => new()
    {
        DraftSlotId = new DraftSlotId(Guid.NewGuid()),
        DraftId = new DraftId(Guid.NewGuid()),
        OverallPick = overall,
        Round = round,
        RoundPick = roundPick,
        TeamId = team.TeamId
    };
}
