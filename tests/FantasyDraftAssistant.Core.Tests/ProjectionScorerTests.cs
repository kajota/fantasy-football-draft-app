using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class ProjectionScorerTests
{
    [Fact]
    public void Scores_using_actual_league_values()
    {
        var rules = RosterRules.DefaultScoring().Select(s => new ScoringRule
        {
            ScoringRuleId = ScoringRuleId.New(),
            LeagueId = LeagueId.New(),
            Category = s.Category,
            Points = s.Points
        }).ToList();

        var projection = new PlayerProjection
        {
            PlayerId = PlayerId.New(),
            SourceKey = "seed",
            PassingYards = 4000,
            PassingTouchdowns = 30,
            Interceptions = 10,
            RushingYards = 200,
            RushingTouchdowns = 3,
            Receptions = 0,
            ReceivingYards = 0,
            ReceivingTouchdowns = 0
        };

        var score = ProjectionScorer.Score(projection, rules);
        // 4000*0.04 + 30*4 + 10*(-2) + 200*0.10 + 3*6 = 160 + 120 - 20 + 20 + 18 = 298
        Assert.Equal(298m, score);
    }

    [Fact]
    public void Receptions_use_league_ppr_value()
    {
        var leagueId = LeagueId.New();
        var rules = new[]
        {
            new ScoringRule { ScoringRuleId = ScoringRuleId.New(), LeagueId = leagueId, Category = ScoringCategory.Reception, Points = 1m },
            new ScoringRule { ScoringRuleId = ScoringRuleId.New(), LeagueId = leagueId, Category = ScoringCategory.ReceivingYard, Points = 0.1m }
        };

        var projection = new PlayerProjection
        {
            PlayerId = PlayerId.New(),
            SourceKey = "seed",
            Receptions = 80,
            ReceivingYards = 1000
        };

        Assert.Equal(180m, ProjectionScorer.Score(projection, rules));
    }
}
