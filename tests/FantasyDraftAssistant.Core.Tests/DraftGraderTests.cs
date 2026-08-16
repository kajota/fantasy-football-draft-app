using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class DraftGraderTests
{
    [Theory]
    [InlineData(98, "A+")]
    [InlineData(93, "A")]
    [InlineData(83, "B")]
    [InlineData(73, "C")]
    [InlineData(50, "F")]
    public void Letter_matches_score_bands(int score, string letter) =>
        Assert.Equal(letter, DraftGrader.Letter(score));

    [Fact]
    public void Empty_team_is_ungraded()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 4, superflex: false);
        Assert.True(DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId)).Succeeded);
        var grades = DraftGrader.Grade(state, [], new Dictionary<PlayerId, PlayerRanking>(),
            new Dictionary<PlayerId, PlayerAdp>(), new Dictionary<PlayerId, PlayerProjection>());
        Assert.All(grades, grade => Assert.Equal("—", grade.Letter));
    }

    [Fact]
    public void Value_and_filled_starters_outrank_reaches_and_holes()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 4, superflex: false);
        Assert.True(DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId)).Succeeded);
        var strongTeam = state.Teams.OrderBy(team => team.DraftPosition).First();
        var weakTeam = state.Teams.OrderBy(team => team.DraftPosition).Skip(1).First();
        var steal = Player("Steal", "ATL", PlayerPosition.RB);
        var reach = Player("Reach", "DAL", PlayerPosition.WR);
        var filler = Player("Filler", "KC", PlayerPosition.WR);
        Pick(state, 1, steal.PlayerId);
        Pick(state, 2, reach.PlayerId);
        Pick(state, 8, filler.PlayerId);

        var now = DateTimeOffset.UtcNow;
        var rankings = new Dictionary<PlayerId, PlayerRanking>();
        var adp = new Dictionary<PlayerId, PlayerAdp>
        {
            [steal.PlayerId] = new() { PlayerId = steal.PlayerId, SourceKey = "test", OverallAdp = 1, CachedAt = now },
            [reach.PlayerId] = new() { PlayerId = reach.PlayerId, SourceKey = "test", OverallAdp = 20, CachedAt = now },
            [filler.PlayerId] = new() { PlayerId = filler.PlayerId, SourceKey = "test", OverallAdp = 2, CachedAt = now }
        };
        var projections = new Dictionary<PlayerId, PlayerProjection>
        {
            [steal.PlayerId] = Proj(steal.PlayerId, rushYards: 1400, rushTd: 12, rec: 50, recYards: 400),
            [reach.PlayerId] = Proj(reach.PlayerId, rec: 60, recYards: 700, recTd: 4),
            [filler.PlayerId] = Proj(filler.PlayerId, rec: 40, recYards: 500, recTd: 3)
        };

        var grades = DraftGrader.Grade(state, [steal, reach, filler], rankings, adp, projections);
        var strong = grades.Single(grade => grade.TeamId.Equals(strongTeam.TeamId));
        var weak = grades.Single(grade => grade.TeamId.Equals(weakTeam.TeamId));
        Assert.True(strong.Score > weak.Score, $"{strong.Letter}/{strong.Score} should beat {weak.Letter}/{weak.Score}");
        Assert.Contains(strong.Notes, note => note.Contains("value", StringComparison.OrdinalIgnoreCase)
                                             || note.Contains("ADP", StringComparison.OrdinalIgnoreCase));
        Assert.True(strong.AverageValue > 0);
        Assert.True(weak.AverageValue < 0);
    }

    private static void Pick(FantasyDraftAssistant.Core.Results.DraftWorkingState state, int overall, PlayerId playerId)
    {
        var slot = state.Slots.First(item => item.OverallPick == overall);
        state.ActiveSelections[overall] = new ActiveSelection
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
            Source = PickSource.Manual,
            ObservedAt = DateTimeOffset.UtcNow
        };
        state.UnavailablePlayers.Add(playerId);
    }

    private static Player Player(string name, string team, PlayerPosition position) => new()
    {
        PlayerId = PlayerId.FromName(name, team, position.ToString()),
        Name = name,
        NflTeam = team,
        PrimaryPosition = position,
        EligiblePositions = [position]
    };

    private static PlayerProjection Proj(
        PlayerId id,
        double rushYards = 0,
        double rushTd = 0,
        double rec = 0,
        double recYards = 0,
        double recTd = 0) => new()
    {
        PlayerId = id,
        SourceKey = "test",
        RushingYards = rushYards,
        RushingTouchdowns = rushTd,
        Receptions = rec,
        ReceivingYards = recYards,
        ReceivingTouchdowns = recTd,
        CachedAt = DateTimeOffset.UtcNow
    };
}
