using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Analytics;

public static class ProjectionScorer
{
    public static decimal Score(PlayerProjection projection, IReadOnlyList<ScoringRule> rules)
    {
        var map = rules.ToDictionary(r => r.Category, r => r.Points);
        decimal points = 0;

        points += Value(map, ScoringCategory.PassingYard) * (decimal)projection.PassingYards;
        points += Value(map, ScoringCategory.PassingTouchdown) * (decimal)projection.PassingTouchdowns;
        points += Value(map, ScoringCategory.Interception) * (decimal)projection.Interceptions;
        points += Value(map, ScoringCategory.RushingYard) * (decimal)projection.RushingYards;
        points += Value(map, ScoringCategory.RushingTouchdown) * (decimal)projection.RushingTouchdowns;
        points += Value(map, ScoringCategory.Reception) * (decimal)projection.Receptions;
        points += Value(map, ScoringCategory.ReceivingYard) * (decimal)projection.ReceivingYards;
        points += Value(map, ScoringCategory.ReceivingTouchdown) * (decimal)projection.ReceivingTouchdowns;

        return Math.Round(points, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal Value(IReadOnlyDictionary<ScoringCategory, decimal> map, ScoringCategory category) =>
        map.GetValueOrDefault(category);
}
