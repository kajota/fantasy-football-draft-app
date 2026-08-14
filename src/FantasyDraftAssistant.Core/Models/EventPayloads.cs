using System.Text.Json.Serialization;
using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Models;

public sealed class PlayerDraftedPayload
{
    [JsonPropertyName("draftSlotId")] public required Guid DraftSlotId { get; init; }
    [JsonPropertyName("overallPick")] public required int OverallPick { get; init; }
    [JsonPropertyName("round")] public required int Round { get; init; }
    [JsonPropertyName("roundPick")] public required int RoundPick { get; init; }
    [JsonPropertyName("teamId")] public required Guid TeamId { get; init; }
    [JsonPropertyName("playerId")] public required Guid PlayerId { get; init; }
    [JsonPropertyName("source")] public required PickSource Source { get; init; }
    [JsonPropertyName("externalSourceId")] public string? ExternalSourceId { get; init; }
    [JsonPropertyName("observedAt")] public required DateTimeOffset ObservedAt { get; init; }
}

public sealed class DraftRolledBackPayload
{
    [JsonPropertyName("targetOverallPick")] public required int TargetOverallPick { get; init; }
    [JsonPropertyName("deactivatedOverallPicks")] public required int[] DeactivatedOverallPicks { get; init; }
}

public sealed class DraftRedonePayload
{
    [JsonPropertyName("restoredOverallPicks")] public required int[] RestoredOverallPicks { get; init; }
}

public sealed class PickCorrectedPayload
{
    [JsonPropertyName("overallPick")] public required int OverallPick { get; init; }
    [JsonPropertyName("originalPlayerId")] public required Guid OriginalPlayerId { get; init; }
    [JsonPropertyName("replacementPlayerId")] public required Guid ReplacementPlayerId { get; init; }
    [JsonPropertyName("originalEventId")] public required Guid OriginalEventId { get; init; }
}

public sealed class DraftBranchCreatedPayload
{
    [JsonPropertyName("newBranchId")] public required Guid NewBranchId { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("parentBranchId")] public required Guid ParentBranchId { get; init; }
    [JsonPropertyName("branchPointOverallPick")] public required int BranchPointOverallPick { get; init; }
}

public sealed class DraftStartedPayload
{
    [JsonPropertyName("leagueId")] public required Guid LeagueId { get; init; }
    [JsonPropertyName("mainBranchId")] public required Guid MainBranchId { get; init; }
}

public sealed class DraftCompletedPayload
{
    [JsonPropertyName("completedAt")] public required DateTimeOffset CompletedAt { get; init; }
}
