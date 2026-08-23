using System.Text.RegularExpressions;

namespace FantasyDraftAssistant.Core.Engine;

public static partial class BoardSlug
{
    public const string DefaultBaseUrl = "https://kellynorton.com/draft";
    public const string CredentialScope = "publish";
    public const string CredentialKey = "bearer";
    public const string BaseUrlSettingKey = "board-publish-base-url";

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex Valid();

    public static bool TryNormalize(string? raw, out string slug)
    {
        slug = "";
        if (string.IsNullOrWhiteSpace(raw))
            return false;

        var trimmed = raw.Trim().ToLowerInvariant().Replace(' ', '-');
        if (!Valid().IsMatch(trimmed))
            return false;

        slug = trimmed;
        return true;
    }

    public static string? PublicUrl(string? baseUrl, string? slug)
    {
        if (!TryNormalize(slug, out var normalized))
            return null;
        var root = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.Trim().TrimEnd('/');
        return $"{root}/{normalized}/";
    }
}
