using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class BackupService(SqliteConnectionFactory factory) : IBackupService
{
    private const int Retention = 20;

    public Task<string> CreateBackupAsync(string reason, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(factory.Paths.DatabasePath))
            return Task.FromResult(string.Empty);

        var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        var safeReason = new string(reason.Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());
        var dest = Path.Combine(factory.Paths.BackupDirectory, $"draft-{stamp}-{safeReason}.db");

        using var source = factory.Open();
        using var backup = factory.Open(dest);
        source.BackupDatabase(backup);

        Trim();
        return Task.FromResult(dest);
    }

    public Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default)
    {
        var files = Directory.Exists(factory.Paths.BackupDirectory)
            ? Directory.GetFiles(factory.Paths.BackupDirectory, "draft-*.db")
            : [];

        var list = files
            .Select(path => new BackupInfo
            {
                Path = path,
                Reason = Path.GetFileNameWithoutExtension(path),
                CreatedAt = File.GetCreationTimeUtc(path)
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<BackupInfo>>(list);
    }

    private void Trim()
    {
        var files = Directory.GetFiles(factory.Paths.BackupDirectory, "draft-*.db")
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.CreationTimeUtc)
            .Skip(Retention);
        foreach (var file in files)
        {
            try { file.Delete(); }
            catch (IOException) { }
        }
    }
}
