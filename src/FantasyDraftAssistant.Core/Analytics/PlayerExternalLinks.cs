using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace FantasyDraftAssistant.Core.Analytics;

public sealed record PlayerSiteLinks(string? FantasyPros, string? Sleeper, string? Yahoo)
{
    public bool HasAny => FantasyPros is not null || Sleeper is not null || Yahoo is not null;
}

public static class PlayerExternalLinks
{
    private static readonly HashSet<string> AmbiguousNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "josh allen",
        "michael thomas",
        "chris jones",
        "brian thomas",
        "jordan love",
        "daniel jones"
    };

    public static PlayerSiteLinks Build(string name, string position, string? sleeperId, string? yahooId) =>
        new(FantasyPros(name, position), Sleeper(name, sleeperId), Yahoo(yahooId));

    public static string FantasyPros(string name, string position)
    {
        var slug = Slug(name);
        if (AmbiguousNames.Contains(name.Trim()) && !string.IsNullOrWhiteSpace(position))
            slug += "-" + position.Trim().ToLowerInvariant();
        return $"https://www.fantasypros.com/nfl/players/{slug}.php";
    }

    public static string? Sleeper(string name, string? sleeperId)
    {
        if (string.IsNullOrWhiteSpace(sleeperId))
            return null;
        return $"https://sleeper.com/nfl/players/{Slug(name)}-{sleeperId.Trim()}";
    }

    public static string? Yahoo(string? yahooId)
    {
        if (string.IsNullOrWhiteSpace(yahooId))
            return null;
        return $"https://sports.yahoo.com/nfl/players/{yahooId.Trim()}";
    }

    public static string Slug(string name)
    {
        var folded = name.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(folded.Length);
        foreach (var ch in folded)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(ch))
                builder.Append(ch);
            else if (ch is ' ' or '-' or '_')
                builder.Append('-');
        }

        var slug = Regex.Replace(builder.ToString(), "-+", "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "player" : slug;
    }
}
