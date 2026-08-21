namespace FantasyDraftAssistant.Core.Analytics;

public static class FantasyDataSourcePicker
{
    public static string? Pick(IEnumerable<string> availableKeys, FantasyDataFormat leagueFormat)
    {
        var keys = availableKeys
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (keys.Count == 0)
            return null;

        var exact = Find(keys, leagueFormat.SourceKey);
        if (exact is not null)
            return exact;

        if (leagueFormat.Superflex)
        {
            return Find(keys, "sleeper")
                   ?? Find(keys, "fantasypros")
                   ?? Find(keys, "seed")
                   ?? keys[0];
        }

        return Find(keys, "fantasypros")
               ?? Find(keys, "sleeper")
               ?? Find(keys, "seed")
               ?? keys[0];
    }

    public static string Describe(string? sourceKey, FantasyDataFormat leagueFormat)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return "no cached ranks";

        if (sourceKey.Equals(leagueFormat.SourceKey, StringComparison.OrdinalIgnoreCase))
            return $"FantasyPros {leagueFormat.DisplayName}";

        var parsed = FantasyDataFormat.TryParseSourceKey(sourceKey);
        if (parsed is not null)
            return $"FantasyPros {parsed.DisplayName}";

        return sourceKey.Trim().ToLowerInvariant() switch
        {
            "fantasypros" => "FantasyPros",
            "sleeper" => "Sleeper",
            "seed" => "offline seed",
            var other => other
        };
    }

    public static string? CompatibilityWarning(string? sourceKey, FantasyDataFormat leagueFormat)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return $"Player data compatibility warning: no cached source is selected for this {leagueFormat.DisplayName} league.";

        var sourceFormat = FantasyDataFormat.TryParseSourceKey(sourceKey);
        var sourceLabel = Describe(sourceKey, leagueFormat);
        if (sourceFormat is null)
            return $"Player data compatibility warning: {sourceLabel} is not tagged as Standard, Half PPR, PPR, 1-QB, or Superflex. This league is {leagueFormat.DisplayName}.";

        if (sourceFormat == leagueFormat)
            return null;

        return $"Player data mismatch: this league is {leagueFormat.DisplayName}, but the selected player data is {sourceLabel}. Rankings, ADP, projections, and AI advice may be wrong.";
    }

    public static string ChooseDisplayLabel(
        IReadOnlyList<string> labels,
        string? currentLabel,
        string? sessionLabel,
        string? leagueLabel)
    {
        if (labels.Count == 0)
            return currentLabel ?? "Sleeper";
        if (currentLabel is not null && Contains(labels, currentLabel))
            return labels.First(label => label.Equals(currentLabel, StringComparison.OrdinalIgnoreCase));
        if (sessionLabel is not null && Contains(labels, sessionLabel))
            return labels.First(label => label.Equals(sessionLabel, StringComparison.OrdinalIgnoreCase));
        if (leagueLabel is not null && Contains(labels, leagueLabel))
            return labels.First(label => label.Equals(leagueLabel, StringComparison.OrdinalIgnoreCase));
        return labels.FirstOrDefault(label => label.StartsWith("FantasyPros", StringComparison.OrdinalIgnoreCase))
               ?? labels[0];
    }

    private static bool Contains(IReadOnlyList<string> labels, string wanted) =>
        labels.Any(label => label.Equals(wanted, StringComparison.OrdinalIgnoreCase));

    private static string? Find(IReadOnlyList<string> keys, string wanted) =>
        keys.FirstOrDefault(key => key.Equals(wanted, StringComparison.OrdinalIgnoreCase));
}
