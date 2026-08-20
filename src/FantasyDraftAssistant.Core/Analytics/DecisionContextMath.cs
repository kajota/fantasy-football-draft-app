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

    /// <summary>Above this the player is called gone; below its mirror, called back.</summary>
    private const double LikelyThreshold = 0.75;

    /// <summary>
    /// Probability the player is off the board before the user's next pick.
    ///
    /// Treats the player's actual draft slot as normally distributed around their ADP, with the
    /// spread taken from disagreement between ranking sources. That is a proxy — rank spread is
    /// not observed draft variance — so the number is a calibrated guess, not a measurement.
    /// Returns null when there is no ADP or no next pick to compare against.
    /// </summary>
    public static double? GoneProbability(double? overallAdp, double? rankStd, int? userNextOverallPick)
    {
        if (overallAdp is not { } adp || userNextOverallPick is not { } next)
            return null;

        return StandardNormalCdf((next - adp) / Sigma(rankStd));
    }

    public static string? For(double? overallAdp, double? rankStd, int? userNextOverallPick) =>
        GoneProbability(overallAdp, rankStd, userNextOverallPick) switch
        {
            null => null,
            >= LikelyThreshold => LikelyGone,
            <= 1 - LikelyThreshold => LikelyBack,
            _ => CoinFlip
        };

    /// <summary>
    /// Spread of the player's likely draft slot. Sources that agree closely still leave room for
    /// one manager to reach, so the floor keeps the curve from becoming a step function.
    /// </summary>
    public static double Sigma(double? rankStd) =>
        Math.Clamp(rankStd ?? 4.0, 2.0, 10.0);

    /// <summary>
    /// Normal CDF via the Abramowitz &amp; Stegun 7.1.26 error-function approximation
    /// (max error ~1.5e-7) — far tighter than the inputs deserve, and dependency free.
    /// </summary>
    private static double StandardNormalCdf(double z)
    {
        var sign = z < 0 ? -1.0 : 1.0;
        var x = Math.Abs(z) / Math.Sqrt(2.0);

        const double p = 0.3275911;
        const double a1 = 0.254829592;
        const double a2 = -0.284496736;
        const double a3 = 1.421413741;
        const double a4 = -1.453152027;
        const double a5 = 1.061405429;

        var t = 1.0 / (1.0 + p * x);
        var poly = t * (a1 + t * (a2 + t * (a3 + t * (a4 + t * a5))));
        var erf = 1.0 - poly * Math.Exp(-x * x);

        return 0.5 * (1.0 + sign * erf);
    }
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
/// <summary>How many players are left in a position's current tier before quality drops.</summary>
public sealed record TierCliff(string Position, int Remaining, int Tier, int? NextTier)
{
    /// <summary>
    /// The next tier is normally just this one plus one, which says nothing, so it is only
    /// named when it skips — that happens once every player of the intervening tier is gone,
    /// and it means the drop below this cliff is steeper than usual.
    /// </summary>
    public bool NextTierIsNotable => NextTier is { } next && next > Tier + 1;

    public string Line => NextTierIsNotable
        ? $"{Position}: {Remaining} left in Tier {Tier}, then Tier {NextTier}"
        : $"{Position}: {Remaining} left in Tier {Tier}";
}

public static class TierCliffSummary
{
    /// <summary>Ordered by position so the AI context stays stable between refreshes.</summary>
    public static IReadOnlyList<string> Build(IReadOnlyList<PlayerSummaryDto> available) =>
        Detail(available).Select(cliff => cliff.Line).ToList();

    /// <summary>Same data structured, so the UI can rank cliffs by how close they are.</summary>
    public static IReadOnlyList<TierCliff> Detail(IReadOnlyList<PlayerSummaryDto> available)
    {
        var cliffs = new List<TierCliff>();
        foreach (var group in available
                     .Where(p => p.Tier is not null)
                     .GroupBy(p => p.Position)
                     .OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            var topTier = group.Min(p => p.Tier!.Value);
            var count = group.Count(p => p.Tier!.Value == topTier);
            var laterTiers = group.Where(p => p.Tier!.Value > topTier).Select(p => p.Tier!.Value).ToList();
            cliffs.Add(new TierCliff(
                group.Key,
                count,
                topTier,
                laterTiers.Count > 0 ? laterTiers.Min() : null));
        }

        return cliffs;
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
