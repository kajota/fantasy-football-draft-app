namespace FantasyDraftAssistant.Data.Database;

public sealed class AppPaths
{
    public AppPaths(string? root = null, string? credentialRoot = null)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        Root = root ?? Path.Combine(home, ".local", "share", "fantasy-draft-assistant");
        DatabasePath = Path.Combine(Root, "draft.db");
        BackupDirectory = Path.Combine(Root, "backups");
        TeamPortraitsDirectory = Path.Combine(Root, "team-portraits");

        // Not under Root: the data root is meant to be synced between machines and
        // the credential file is only decryptable on the machine that wrote it.
        CredentialRoot = AppDataRoot.CredentialRoot(credentialRoot);
        CredentialPath = Path.Combine(CredentialRoot, "credentials.dat");

        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(TeamPortraitsDirectory);
        Directory.CreateDirectory(CredentialRoot);
    }

    public string Root { get; }
    public string CredentialRoot { get; }
    public string DatabasePath { get; }
    public string BackupDirectory { get; }
    public string CredentialPath { get; }
    public string TeamPortraitsDirectory { get; }
}
