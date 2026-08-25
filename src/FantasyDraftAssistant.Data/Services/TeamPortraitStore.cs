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
        foreach (var ext in TeamPortraitImage.Extensions)
        {
            var path = Path.Combine(_directory, teamId + ext);
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public string? LastPrompt(TeamId teamId)
    {
        var path = PromptPath(teamId);
        if (!File.Exists(path))
            return null;
        var text = File.ReadAllText(path);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    public async Task SaveAsync(TeamId teamId, byte[] imageBytes, string? prompt = null, CancellationToken cancellationToken = default)
    {
        var ext = TeamPortraitImage.ExtensionOrJpeg(imageBytes);
        var path = Path.Combine(_directory, teamId + ext);
        foreach (var other in TeamPortraitImage.Extensions)
        {
            var stale = Path.Combine(_directory, teamId + other);
            if (!stale.Equals(path, StringComparison.OrdinalIgnoreCase) && File.Exists(stale))
                File.Delete(stale);
        }

        await File.WriteAllBytesAsync(path, imageBytes, cancellationToken);
        var promptPath = PromptPath(teamId);
        if (string.IsNullOrWhiteSpace(prompt))
        {
            if (File.Exists(promptPath))
                File.Delete(promptPath);
        }
        else
        {
            await File.WriteAllTextAsync(promptPath, prompt.Trim(), cancellationToken);
        }

        Changed?.Invoke(this, teamId);
    }

    private string PromptPath(TeamId teamId) => Path.Combine(_directory, teamId + ".prompt.txt");

    public string? CopyTo(TeamId teamId, string destinationPath)
    {
        var source = ExistingPath(teamId);
        if (source is null)
            return null;
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath) ?? ".");
        File.Copy(source, destinationPath, overwrite: true);
        return destinationPath;
    }
}
