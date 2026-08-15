using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Interfaces;

public interface ITeamPortraitStore
{
    string? ExistingPath(TeamId teamId);
    bool Exists(TeamId teamId);
    Task SaveAsync(TeamId teamId, byte[] imageBytes, CancellationToken cancellationToken = default);
    event EventHandler<TeamId>? Changed;
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
    public TeamPortraitTone Tone => TeamPortraitPrompt.ToneFor(IsUserTeam, NormalImage);
}

public sealed class TeamPortraitResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public string? Provider { get; init; }
    public string? Model { get; init; }
}
