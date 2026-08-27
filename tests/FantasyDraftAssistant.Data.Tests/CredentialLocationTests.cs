using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Tests;

/// The credential file is encrypted with a key derived from the machine name, so it
/// must never live in the data root — that root is deliberately synced between
/// machines, and a synced credential file is readable on exactly one of them.
public class CredentialLocationTests
{
    [Fact]
    public void Credentials_do_not_follow_the_data_root()
    {
        var synced = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"), "SynologyDrive");
        var local = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"), "local");

        var paths = new AppPaths(synced, local);

        Assert.StartsWith(synced, paths.DatabasePath);
        Assert.StartsWith(synced, paths.BackupDirectory);
        Assert.StartsWith(synced, paths.TeamPortraitsDirectory);
        Assert.DoesNotContain(synced, paths.CredentialPath);
        Assert.StartsWith(local, paths.CredentialPath);
    }

    [Fact]
    public void Credentials_default_to_the_machine_local_directory()
    {
        var synced = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));

        var paths = new AppPaths(synced);

        Assert.Equal(
            Path.Combine(AppDataRoot.DefaultRoot(), "credentials.dat"),
            paths.CredentialPath);
    }

    [Fact]
    public void An_explicit_credential_root_wins_over_the_default()
    {
        var chosen = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));

        Assert.Equal(Path.GetFullPath(chosen), AppDataRoot.CredentialRoot(chosen));
    }
}
