using System.Reflection;

namespace FantasyDraftAssistant.App;

/// <summary>
/// The version stamped into this build by MinVer, derived from the nearest git tag.
/// </summary>
public static class AppVersion
{
    /// <summary>Full informational version, e.g. "0.5.1-alpha.0.3+fab3bb6". Shown in tooltips and logs.</summary>
    public static string Full { get; } =
        typeof(AppVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? "0.0.0";

    /// <summary>Version without the build metadata suffix, e.g. "0.5.1-alpha.0.3".</summary>
    public static string Display { get; } = Full.Split('+')[0];

    /// <summary>The short commit hash, or null when the build carries no source revision.</summary>
    public static string? Commit { get; } =
        Full.Split('+') is [_, var sha] && sha.Length >= 7 ? sha[..7] : null;
}
