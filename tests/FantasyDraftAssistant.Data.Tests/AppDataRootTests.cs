using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class AppDataRootTests
{
    [Fact]
    public void Explicit_path_wins_over_environment_and_config()
    {
        var home = Path.Combine(Path.GetTempPath(), "fda-home-" + Guid.NewGuid().ToString("N"));
        var config = Path.Combine(home, "config", "data-root");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, "~/from-config");

        var resolved = AppDataRoot.Resolve(
            explicitRoot: "~/from-explicit",
            environmentValue: "~/from-env",
            configFilePath: config,
            userProfile: home);

        Assert.Equal(Path.GetFullPath(Path.Combine(home, "from-explicit")), resolved);
    }

    [Fact]
    public void Environment_wins_over_config()
    {
        var home = Path.Combine(Path.GetTempPath(), "fda-home-" + Guid.NewGuid().ToString("N"));
        var config = Path.Combine(home, "config", "data-root");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, "~/from-config");

        var resolved = AppDataRoot.Resolve(
            explicitRoot: null,
            environmentValue: "~/from-env",
            configFilePath: config,
            userProfile: home);

        Assert.Equal(Path.GetFullPath(Path.Combine(home, "from-env")), resolved);
    }

    [Fact]
    public void Config_file_skips_comments_and_expands_tilde()
    {
        var home = Path.Combine(Path.GetTempPath(), "fda-home-" + Guid.NewGuid().ToString("N"));
        var config = Path.Combine(home, "config", "data-root");
        Directory.CreateDirectory(Path.GetDirectoryName(config)!);
        File.WriteAllText(config, """
            # Close the app before switching computers.
            ~/SynologyDrive/fantasy-draft-assistant
            """);

        var resolved = AppDataRoot.Resolve(
            explicitRoot: "  ",
            environmentValue: null,
            configFilePath: config,
            userProfile: home);

        Assert.Equal(Path.GetFullPath(Path.Combine(home, "SynologyDrive", "fantasy-draft-assistant")), resolved);
    }

    [Fact]
    public void Missing_config_falls_back_to_default_under_home()
    {
        var home = Path.Combine(Path.GetTempPath(), "fda-home-" + Guid.NewGuid().ToString("N"));
        var resolved = AppDataRoot.Resolve(
            explicitRoot: null,
            environmentValue: null,
            configFilePath: Path.Combine(home, "missing", "data-root"),
            userProfile: home);

        Assert.Equal(Path.GetFullPath(Path.Combine(home, ".local", "share", "fantasy-draft-assistant")), resolved);
    }

    [Fact]
    public void AddFantasyDraftData_uses_explicit_root()
    {
        var root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(root);
        using var services = collection.BuildServiceProvider();
        Assert.Equal(Path.GetFullPath(root), services.GetRequiredService<AppPaths>().Root);
    }
}
