using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class TeamPortraitStore : ITeamPortraitStore
{
    private readonly string _directory;

    public TeamPortraitStore(AppPaths paths)
    {
        _directory = paths.TeamPortraitsDirectory;
        Directory.CreateDirectory(_directory);
    }

    public event EventHandler<TeamId>? Changed;

    public bool Exists(TeamId teamId) => ExistingPath(teamId) is not null;

    public string? ExistingPath(TeamId teamId)
    {
        foreach (var ext in new[] { ".jpg", ".jpeg", ".png", ".webp" })
        {
            var path = Path.Combine(_directory, teamId + ext);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public async Task SaveAsync(TeamId teamId, byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var ext = Extension(imageBytes);
        var path = Path.Combine(_directory, teamId + ext);
        foreach (var other in new[] { ".jpg", ".jpeg", ".png", ".webp" })
        {
            var stale = Path.Combine(_directory, teamId + other);
            if (!stale.Equals(path, StringComparison.OrdinalIgnoreCase) && File.Exists(stale))
                File.Delete(stale);
        }

        await File.WriteAllBytesAsync(path, imageBytes, cancellationToken);
        Changed?.Invoke(this, teamId);
    }

    public string? CopyTo(TeamId teamId, string destinationPath)
    {
        var source = ExistingPath(teamId);
        if (source is null)
            return null;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        File.Copy(source, destinationPath, overwrite: true);
        return destinationPath;
    }

    private static string Extension(byte[] bytes)
    {
        if (bytes.Length >= 8
            && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            return ".png";
        if (bytes.Length >= 12
            && bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46
            && bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            return ".webp";
        return ".jpg";
    }
}
