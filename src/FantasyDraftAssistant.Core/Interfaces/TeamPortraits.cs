using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Interfaces;

public interface ITeamPortraitStore
{
    string? ExistingPath(TeamId teamId);
    bool Exists(TeamId teamId);
    Task SaveAsync(TeamId teamId, byte[] imageBytes, CancellationToken cancellationToken = default);
    string? CopyTo(TeamId teamId, string destinationPath);
    event EventHandler<TeamId>? Changed;
}

public interface IFileSavePicker
{
    Task<string?> PickSavePathAsync(string suggestedFileName, string defaultExtension, CancellationToken cancellationToken = default);
}

public static class TeamPortraitFiles
{
    public static string SuggestedFileName(string teamName, string sourcePath)
    {
        var ext = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(ext))
            ext = ".jpg";
        var name = string.Join("_", teamName.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = "team";
        return name + ext;
    }
}

public interface ITeamPortraitGenerator
{
    Task<TeamPortraitResult> GenerateAsync(TeamPortraitRequest request, CancellationToken cancellationToken = default);
}

public sealed class TeamPortraitRequest
{
    public required TeamId TeamId { get; init; }
    public required string TeamName { get; init; }
    public string? OwnerName { get; init; }
    public required bool IsUserTeam { get; init; }
    public bool NormalImage { get; init; }
    public string? ProviderKey { get; init; }
    public TeamPortraitTone Tone => TeamPortraitPrompt.ToneFor(IsUserTeam, NormalImage);
}

public sealed class TeamPortraitResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
}
