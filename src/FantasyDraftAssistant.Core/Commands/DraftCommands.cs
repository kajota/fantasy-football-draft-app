using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Commands;

public sealed record StartDraftCommand(
    DraftId DraftId,
    string CreatedBy = "user");

public sealed record DraftPlayerCommand(
    DraftId DraftId,
    PlayerId PlayerId,
    PickSource Source = PickSource.Manual,
    string? ExternalSourceId = null,
    DateTimeOffset? ObservedAt = null,
    string CreatedBy = "user");

public sealed record RollbackDraftCommand(
    DraftId DraftId,
    int TargetOverallPick,
    string CreatedBy = "user");

public sealed record RedoDraftCommand(
    DraftId DraftId,
    string CreatedBy = "user");

public sealed record CorrectPickCommand(
    DraftId DraftId,
    int OverallPick,
    PlayerId ReplacementPlayerId,
    string CreatedBy = "user");

public sealed record CreateDraftBranchCommand(
    DraftId DraftId,
    string Name,
    int BranchPointOverallPick,
    bool SwitchToNewBranch = true,
    string CreatedBy = "user");

public sealed record SwitchDraftBranchCommand(
    DraftId DraftId,
    BranchId BranchId,
    string CreatedBy = "user");

public sealed record CompleteDraftCommand(
    DraftId DraftId,
    string CreatedBy = "user");

public sealed record QueueAddCommand(
    DraftId DraftId,
    PlayerId PlayerId);

public sealed record QueueRemoveCommand(
    DraftId DraftId,
    QueueItemId QueueItemId);

public sealed record QueueReorderCommand(
    DraftId DraftId,
    IReadOnlyList<QueueItemId> OrderedIds);

public sealed class CreateLeagueRequest
{
    public required string Name { get; init; }
    public int Season { get; init; } = DateTime.UtcNow.Year;
    public int TeamCount { get; init; } = 12;
    public DraftType DraftType { get; init; } = DraftType.Snake;
    public int RoundCount { get; init; } = 15;
    public bool Superflex { get; init; }
    public FantasyPlatform Platform { get; init; } = FantasyPlatform.Manual;
    public string? UserTeamName { get; init; }
}

public sealed class SaveTeamsRequest
{
    public required LeagueId LeagueId { get; init; }
    public required IReadOnlyList<TeamDraftPosition> Teams { get; init; }
    public TeamId? UserTeamId { get; init; }
}

public sealed record TeamDraftPosition
{
    public TeamId? TeamId { get; init; }
    public required string Name { get; init; }
    public string? OwnerName { get; init; }
    public string? DisplayLabel { get; init; }
    public required int DraftPosition { get; init; }
}

public sealed class SaveRosterRequest
{
    public required LeagueId LeagueId { get; init; }
    public required IReadOnlyList<RosterSlotSpec> Slots { get; init; }
}

public sealed class RosterSlotSpec
{
    public required string SlotCode { get; init; }
    public required SlotKind SlotKind { get; init; }
    public required int Count { get; init; }
    public required IReadOnlyList<PlayerPosition> EligiblePositions { get; init; }
}

public sealed class SaveScoringRequest
{
    public required LeagueId LeagueId { get; init; }
    public required IReadOnlyList<ScoringRuleSpec> Rules { get; init; }
}

public sealed class ScoringRuleSpec
{
    public required ScoringCategory Category { get; init; }
    public required decimal Points { get; init; }
}

public sealed class SaveKeepersRequest
{
    public required DraftId DraftId { get; init; }
    public required IReadOnlyList<KeeperSpec> Keepers { get; init; }
}

public sealed class KeeperSpec
{
    public required TeamId TeamId { get; init; }
    public required PlayerId PlayerId { get; init; }
    public required int RoundCost { get; init; }
    public string? Notes { get; init; }
}

public sealed class CreateDraftRequest
{
    public required LeagueId LeagueId { get; init; }
    public required string Name { get; init; }
}
