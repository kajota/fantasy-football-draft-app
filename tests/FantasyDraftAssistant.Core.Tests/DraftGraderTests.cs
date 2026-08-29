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

    [Fact]
    public void Room_wide_adp_bias_does_not_fail_every_team()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 4, superflex: true);
        Assert.True(DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId)).Succeeded);
        var now = DateTimeOffset.UtcNow;
        var players = new List<Player>();
        var adp = new Dictionary<PlayerId, PlayerAdp>();
        var projections = new Dictionary<PlayerId, PlayerProjection>();
        for (var pick = 1; pick <= 4; pick++)
        {
            var player = Player($"QB{pick}", "BUF", PlayerPosition.QB);
            players.Add(player);
            Pick(state, pick, player.PlayerId);
            adp[player.PlayerId] = new PlayerAdp
            {
                PlayerId = player.PlayerId,
                SourceKey = "test",
                OverallAdp = pick + 30,
                CachedAt = now
            };
            projections[player.PlayerId] = Proj(player.PlayerId, rushYards: 200, rushTd: 2);
        }

        var grades = DraftGrader.Grade(state, players, new Dictionary<PlayerId, PlayerRanking>(), adp, projections)
            .Where(grade => grade.Letter != "—")
            .ToList();
        Assert.True(grades.Count >= 2);
        Assert.True(grades.Max(grade => grade.Score) - grades.Min(grade => grade.Score) <= 3,
            string.Join(", ", grades.Select(grade => $"{grade.TeamName}:{grade.Letter}{grade.Score}")));
    }

    [Fact]
    public void Missing_kicker_and_defense_do_not_lower_the_grade()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 7, superflex: false);
        Assert.True(DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId)).Succeeded);
        var team = state.Teams.OrderBy(t => t.DraftPosition).First();
        var teamPicks = state.Slots
            .Where(slot => slot.TeamId.Equals(team.TeamId))
            .OrderBy(slot => slot.Round)
            .Select(slot => slot.OverallPick)
            .ToList();
        Assert.Equal(7, teamPicks.Count);

        // Fills every non-K/DEF starting slot (QB, RB, RB, WR, WR, TE, FLEX) and drafts
        // neither a kicker nor a defense — a real, common draft-day plan.
        var positions = new[]
        {
            PlayerPosition.QB, PlayerPosition.RB, PlayerPosition.RB,
            PlayerPosition.WR, PlayerPosition.WR, PlayerPosition.TE, PlayerPosition.RB
        };
        var players = new List<Player>();
        for (var i = 0; i < teamPicks.Count; i++)
        {
            var player = Player($"Starter{i}", "KC", positions[i]);
            players.Add(player);
            Pick(state, teamPicks[i], player.PlayerId);
        }

        var grades = DraftGrader.Grade(state, players, new Dictionary<PlayerId, PlayerRanking>(),
            new Dictionary<PlayerId, PlayerAdp>(), new Dictionary<PlayerId, PlayerProjection>());
        var grade = grades.Single(g => g.TeamId.Equals(team.TeamId));

        Assert.Equal(0, grade.OpenStarters);
    }

    [Fact]
    public void A_kicker_reach_does_not_affect_the_value_grade()
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 2, superflex: false);
        Assert.True(DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId)).Succeeded);
        var team = state.Teams.OrderBy(t => t.DraftPosition).First();
        var teamPicks = state.Slots
            .Where(slot => slot.TeamId.Equals(team.TeamId))
            .OrderBy(slot => slot.Round)
            .Select(slot => slot.OverallPick)
            .ToList();

        var solid = Player("Solid", "KC", PlayerPosition.WR);
        var kickerReach = Player("EarlyKicker", "SF", PlayerPosition.K);
        Pick(state, teamPicks[0], solid.PlayerId);
        Pick(state, teamPicks[1], kickerReach.PlayerId);

        var now = DateTimeOffset.UtcNow;
        var adp = new Dictionary<PlayerId, PlayerAdp>
        {
            [solid.PlayerId] = new() { PlayerId = solid.PlayerId, SourceKey = "test", OverallAdp = teamPicks[0], CachedAt = now },
            // A kicker drafted this early vs. a normal late kicker ADP would otherwise
            // register as a huge reach and tank the value grade.
            [kickerReach.PlayerId] = new() { PlayerId = kickerReach.PlayerId, SourceKey = "test", OverallAdp = 180, CachedAt = now }
        };

        var grades = DraftGrader.Grade(state, [solid, kickerReach], new Dictionary<PlayerId, PlayerRanking>(),
            adp, new Dictionary<PlayerId, PlayerProjection>());
        var grade = grades.Single(g => g.TeamId.Equals(team.TeamId));

        Assert.DoesNotContain(grade.Notes, note => note.Contains(kickerReach.Name, StringComparison.Ordinal));
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
        PlayerId = PlayerId.FromName(name, position.ToString()),
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
