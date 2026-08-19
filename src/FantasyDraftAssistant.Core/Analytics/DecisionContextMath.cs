using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.Core.Analytics;

/// <summary>
/// Chance an available player survives until the user's next pick.
/// Deterministic so the app, not the AI, does the arithmetic.
/// </summary>
public static class PickOutlook
{
    public const string LikelyGone = "likely gone";
    public const string CoinFlip = "coin flip";
    public const string LikelyBack = "likely back";

    public static string? For(double? overallAdp, double? rankStd, int? userNextOverallPick)
    {
        if (overallAdp is not { } adp || userNextOverallPick is not { } next)
            return null;

        var margin = Margin(rankStd);
        if (adp + margin < next)
            return LikelyGone;
        if (adp - margin > next)
            return LikelyBack;
        return CoinFlip;
    }

    public static double Margin(double? rankStd) =>
        rankStd is { } std ? Math.Clamp(std * 1.5, 4.0, 12.0) : 6.0;
}

/// <summary>
/// Projected points relative to the replacement-level starter at each position.
/// The baseline is the Nth-best available player at a position, where N is the
/// league-wide count of unfilled starter slots for that position. Flex slots
/// count toward every eligible position, so the baseline is slightly deep on
/// purpose; it is a comparison anchor, not a projection.
/// </summary>
public static class ValueOverReplacement
{
    public static IReadOnlyDictionary<string, decimal> Baselines(
        IReadOnlyList<PlayerSummaryDto> available,
        IReadOnlyDictionary<string, int> remainingLeagueDemand)
    {
        var baselines = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in available
                     .Where(p => p.ProjectedPoints is not null)
                     .GroupBy(p => p.Position, StringComparer.OrdinalIgnoreCase))
        {
            var demand = remainingLeagueDemand.GetValueOrDefault(group.Key);
            if (demand <= 0)
                continue;

            var ordered = group.OrderByDescending(p => p.ProjectedPoints!.Value).ToList();
            var baselineIndex = Math.Min(demand, ordered.Count) - 1;
            baselines[group.Key] = ordered[baselineIndex].ProjectedPoints!.Value;
        }

        return baselines;
    }
}

/// <summary>
/// One line per position describing how many players remain in the best
/// remaining tier, so the AI can see tier cliffs without counting.
/// </summary>
public static class TierCliffSummary
{
    public static IReadOnlyList<string> Build(IReadOnlyList<PlayerSummaryDto> available)
    {
        var lines = new List<string>();
        foreach (var group in available
                     .Where(p => p.Tier is not null)
                     .GroupBy(p => p.Position)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var topTier = group.Min(p => p.Tier!.Value);
            var count = group.Count(p => p.Tier!.Value == topTier);
            var laterTiers = group.Where(p => p.Tier!.Value > topTier).Select(p => p.Tier!.Value).ToList();
            lines.Add(laterTiers.Count > 0
                ? $"{group.Key}: {count} left in Tier {topTier}, next tier is {laterTiers.Min()}"
                : $"{group.Key}: {count} left in Tier {topTier}, no later tier cached");
        }

        return lines;
    }
}

/// <summary>
/// Human-readable data age ("14h ago") so the AI never does date arithmetic.
/// </summary>
public static class FreshnessAge
{
    public static string Describe(DateTimeOffset refreshedAt, DateTimeOffset now)
    {
        var age = now - refreshedAt;
        if (age.TotalMinutes < 1)
            return "just now";
        if (age.TotalMinutes < 60)
            return $"{(int)age.TotalMinutes}m ago";
        if (age.TotalHours < 48)
            return $"{(int)age.TotalHours}h ago";
        return $"{(int)age.TotalDays}d ago";
    }
}
