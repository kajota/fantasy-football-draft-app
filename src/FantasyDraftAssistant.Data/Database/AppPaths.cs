namespace FantasyDraftAssistant.Data.Database;

public sealed class AppPaths
{
    public AppPaths(string? root = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Root = root ?? Path.Combine(home, ".local", "share", "fantasy-draft-assistant");
        DatabasePath = Path.Combine(Root, "draft.db");
        BackupDirectory = Path.Combine(Root, "backups");
        CredentialPath = Path.Combine(Root, "credentials.dat");
        TeamPortraitsDirectory = Path.Combine(Root, "team-portraits");
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(TeamPortraitsDirectory);
    }

    public string Root { get; }
    public string DatabasePath { get; }
    public string BackupDirectory { get; }
    public string CredentialPath { get; }
    public string TeamPortraitsDirectory { get; }
}
