namespace FantasyDraftAssistant.Data.Database;

/// <summary>
/// Resolves the on-disk data directory. Explicit path wins, then
/// <c>FANTASY_DRAFT_ASSISTANT_DATA</c>, then <c>~/.config/fantasy-draft-assistant/data-root</c>,
/// then <c>~/.local/share/fantasy-draft-assistant</c>.
/// </summary>
public static class AppDataRoot
{
    public const string EnvironmentVariableName = "FANTASY_DRAFT_ASSISTANT_DATA";
    public const string CredentialEnvironmentVariableName = "FANTASY_DRAFT_ASSISTANT_CREDENTIALS";
    public const string ConfigFileName = "data-root";

    /// <summary>
    /// Where the fallback credential file lives. Deliberately independent of the
    /// data root: league data is meant to be synced between machines, but the
    /// credential file is encrypted with a key derived from the machine name, so
    /// syncing it makes every machine but one read an empty credential store.
    /// API keys are per-machine by design — entered once on each.
    /// </summary>
    public static string CredentialRoot(string? explicitRoot = null, string? userProfile = null)
    {
        var home = Profile(userProfile);

        if (TryNormalize(explicitRoot, home, out var fromExplicit))
            return fromExplicit;

        var fromEnvironment = Environment.GetEnvironmentVariable(CredentialEnvironmentVariableName);
        if (TryNormalize(fromEnvironment, home, out var resolved))
            return resolved;

        return Path.GetFullPath(DefaultRoot(home));
    }

    public static string DefaultRoot(string? userProfile = null)
    {
        var home = Profile(userProfile);
        return Path.Combine(home, ".local", "share", "fantasy-draft-assistant");
    }

    public static string ConfigDirectory(string? userProfile = null)
    {
        var home = Profile(userProfile);
        return Path.Combine(home, ".config", "fantasy-draft-assistant");
    }

    public static string ConfigPath(string? userProfile = null) =>
        Path.Combine(ConfigDirectory(userProfile), ConfigFileName);

    public static string ResolveFromEnvironment(string? explicitRoot = null) =>
        Resolve(explicitRoot, Environment.GetEnvironmentVariable(EnvironmentVariableName), ConfigPath());

    public static string Resolve(
        string? explicitRoot,
        string? environmentValue,
        string? configFilePath,
        string? userProfile = null)
    {
        var home = Profile(userProfile);

        if (TryNormalize(explicitRoot, home, out var fromExplicit))
            return fromExplicit;

        if (TryNormalize(environmentValue, home, out var fromEnv))
            return fromEnv;

        if (TryNormalize(ReadConfigFile(configFilePath), home, out var fromConfig))
            return fromConfig;

        return Path.GetFullPath(DefaultRoot(home));
    }

    private static string Profile(string? userProfile) =>
        string.IsNullOrWhiteSpace(userProfile)
            ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
            : userProfile;

    private static string? ReadConfigFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;

        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                continue;
            return trimmed.Trim('"');
        }

        return null;
    }

    private static bool TryNormalize(string? path, string home, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(path))
            return false;

        var trimmed = path.Trim().Trim('"');
        if (trimmed.Length == 0)
            return false;

        if (trimmed == "~")
            trimmed = home;
        else if (trimmed.StartsWith("~/") || trimmed.StartsWith("~\\"))
            trimmed = Path.Combine(home, trimmed[2..].Replace('\\', Path.DirectorySeparatorChar));

        normalized = Path.GetFullPath(trimmed);
        return true;
    }
}
