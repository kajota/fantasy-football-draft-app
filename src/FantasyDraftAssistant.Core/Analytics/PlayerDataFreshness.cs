using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Analytics;

/// <summary>
/// Draft Room warning when the selected player-data cache is missing or older than a day.
/// </summary>
public static class PlayerDataFreshness
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    public static string? Warning(
        IEnumerable<FantasyDataRefreshInfo> refreshes,
        string? sourceKey,
        DateTimeOffset now,
        TimeZoneInfo? zone = null)
    {
        var latest = LatestFor(refreshes, sourceKey);
        if (latest is null)
            return "Player data has not been synced. Refresh on Player Data so ranks, ADP, and injury status stay current.";

        if (now - latest.Value < MaxAge)
            return null;

        return $"Player data last synced {FreshnessAge.Describe(latest.Value, now)} ({LocalClock.Format(latest.Value, zone)}). Refresh on Player Data so ranks, ADP, and injury status stay current.";
    }

    public static string Combine(params string?[] parts) =>
        string.Join(" ", parts.Where(static part => !string.IsNullOrWhiteSpace(part)));

    public static DateTimeOffset? LatestFor(IEnumerable<FantasyDataRefreshInfo> refreshes, string? sourceKey)
    {
        var provider = ProviderKeyFor(sourceKey);
        var relevant = refreshes
            .Where(refresh => provider is null
                || refresh.ProviderKey.Equals(provider, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (relevant.Count == 0)
            return null;
        return relevant.Max(refresh => refresh.RefreshedAt);
    }

    private static string? ProviderKeyFor(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return null;
        var key = sourceKey.Trim().ToLowerInvariant();
        return key.StartsWith("fantasypros", StringComparison.Ordinal) ? "fantasypros" : key;
    }
}
