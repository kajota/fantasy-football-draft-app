using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Models;

public sealed class League
{
    public required LeagueId LeagueId { get; init; }
    public required string Name { get; set; }
    public FantasyPlatform Platform { get; set; } = FantasyPlatform.Manual;
    public string? ExternalLeagueId { get; set; }
    public int Season { get; set; }
    public int TeamCount { get; set; }
    public TeamId? UserTeamId { get; set; }
    public DraftType DraftType { get; set; } = DraftType.Snake;
    public int RoundCount { get; set; }
    public int RosterSize { get; set; }
    public DraftSourcePreference DraftSourcePreference { get; set; } = DraftSourcePreference.Manual;
    public string? DraftGuidelines { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ArchivedAt { get; set; }
}

public sealed class Team
{
    public required TeamId TeamId { get; init; }
    public required LeagueId LeagueId { get; init; }
    public required string Name { get; set; }
    public string? OwnerName { get; set; }
    public string? DisplayLabel { get; set; }
    public int DraftPosition { get; set; }
    public string? ExternalTeamId { get; set; }

    public string Label => string.IsNullOrWhiteSpace(DisplayLabel) ? Name : DisplayLabel;
}

public sealed class RosterSlot
{
    public required RosterSlotId RosterSlotId { get; init; }
    public required LeagueId LeagueId { get; init; }
    public required string SlotCode { get; set; }
    public SlotKind SlotKind { get; set; }
    public int Count { get; set; }
    public required IReadOnlyList<PlayerPosition> EligiblePositions { get; set; }
}

public sealed class ScoringRule
{
    public required ScoringRuleId ScoringRuleId { get; init; }
    public required LeagueId LeagueId { get; init; }
    public required ScoringCategory Category { get; init; }
    public decimal Points { get; set; }
}

public sealed class Player
{
    public required PlayerId PlayerId { get; init; }
    public required string Name { get; set; }
    public required string NflTeam { get; set; }
    public required PlayerPosition PrimaryPosition { get; set; }
    public required IReadOnlyList<PlayerPosition> EligiblePositions { get; set; }
    public int? ByeWeek { get; set; }
    public int? YearsExp { get; set; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Active;
    public DateTimeOffset? StatusUpdatedAt { get; set; }
    public string? InjuryBodyPart { get; set; }
    public string? InjuryNotes { get; set; }
    public string? InjuryStartedOn { get; set; }

    public bool IsRookie => YearsExp == 0;
    public bool IsInjured => Status != PlayerStatus.Active;

    public string StatusCode => Status switch
    {
        PlayerStatus.Questionable => "Q",
        PlayerStatus.Doubtful => "D",
        PlayerStatus.Out => "O",
        PlayerStatus.InjuredReserve => "IR",
        PlayerStatus.PhysicallyUnableToPerform => "PUP",
        PlayerStatus.Suspended => "SUS",
        PlayerStatus.NonFootballInjury => "NFI",
        _ => ""
    };

    public string InjuryLine
    {
        get
        {
            var parts = new List<string>();
            if (IsInjured)
                parts.Add(Status.ToString());
            if (!string.IsNullOrWhiteSpace(InjuryBodyPart))
                parts.Add(InjuryBodyPart);
            if (!string.IsNullOrWhiteSpace(InjuryNotes))
                parts.Add(InjuryNotes);
            if (!string.IsNullOrWhiteSpace(InjuryStartedOn))
                parts.Add($"since {InjuryStartedOn}");
            return string.Join(" · ", parts);
        }
    }
}

public sealed class Draft
{
    public required DraftId DraftId { get; init; }
    public required LeagueId LeagueId { get; init; }
    public required string Name { get; set; }
    public int Season { get; set; }
    public DraftStatus Status { get; set; } = DraftStatus.NotStarted;
    public BranchId ActiveBranchId { get; set; }
    public int CurrentStateVersion { get; set; }
    public DraftSourceMode SourceMode { get; set; } = DraftSourceMode.Manual;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class DraftBranch
{
    public required BranchId BranchId { get; init; }
    public required DraftId DraftId { get; init; }
    public required string Name { get; set; }
    public BranchId? ParentBranchId { get; init; }
    public int BranchPointOverallPick { get; init; }
    public int CreatedFromStateVersion { get; init; }
    public EventId? CurrentHeadEventId { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class DraftSlot
{
    public required DraftSlotId DraftSlotId { get; init; }
    public required DraftId DraftId { get; init; }
    public int OverallPick { get; init; }
    public int Round { get; init; }
    public int RoundPick { get; init; }
    public required TeamId TeamId { get; set; }
    public bool IsKeeperSlot { get; set; }
}

public sealed class MockSeatPolicy
{
    public required TeamId TeamId { get; init; }
    public required MockPersonality Personality { get; init; }
    public required bool IsCpu { get; init; }
}

public sealed class MockPickResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public bool IsUserPick { get; init; }
    public bool IsComplete { get; init; }
    public int PicksMade { get; init; }
    public string? PlayerName { get; init; }
    public string? TeamName { get; init; }
    public string? Personality { get; init; }
    public BranchId? BranchId { get; init; }

    public static MockPickResult Fail(string error) => new()
    {
        Succeeded = false,
        Error = error
    };

    public static MockPickResult UserPick() => new()
    {
        Succeeded = true,
        IsUserPick = true,
        Error = "It's your pick."
    };

    public static MockPickResult Complete() => new()
    {
        Succeeded = true,
        IsComplete = true,
        Error = "The draft is complete."
    };
}

public sealed class Keeper
{
    public required KeeperId KeeperId { get; init; }
    public required DraftId DraftId { get; init; }
    public required TeamId TeamId { get; init; }
    public required PlayerId PlayerId { get; set; }
    public int RoundCost { get; set; }
    public DraftSlotId? DraftSlotId { get; set; }
    public string? Notes { get; set; }
}

public sealed class DraftQueueItem
{
    public required QueueItemId QueueItemId { get; init; }
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
    public required PlayerId PlayerId { get; init; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class ActiveSelection
{
    public required EventId EventId { get; init; }
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
    public required DraftSlotId DraftSlotId { get; init; }
    public int OverallPick { get; init; }
    public int Round { get; init; }
    public int RoundPick { get; init; }
    public required TeamId TeamId { get; init; }
    public required PlayerId PlayerId { get; init; }
    public PickSource Source { get; init; }
    public string? ExternalSourceId { get; init; }
    public DateTimeOffset ObservedAt { get; init; }
    public bool IsActive { get; set; } = true;
}

public sealed class DraftEventRecord
{
    public required EventId EventId { get; init; }
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
    public required DraftEventType EventType { get; init; }
    public int StateVersion { get; init; }
    public int SequenceNumber { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public string CreatedBy { get; init; } = "user";
    public string? CorrelationId { get; init; }
    public required string PayloadJson { get; init; }
}

public sealed class RedoCandidate
{
    public required IReadOnlyList<ActiveSelection> Selections { get; init; }
    public int RolledBackToOverallPick { get; init; }
}

public sealed class PlayerRanking
{
    public required PlayerId PlayerId { get; init; }
    public required string SourceKey { get; init; }
    public int OverallRank { get; init; }
    public int? PositionRank { get; init; }
    public int? Tier { get; init; }
    public int? RankMin { get; init; }
    public int? RankMax { get; init; }
    public double? RankStd { get; init; }
    public DateTimeOffset? SourceTimestamp { get; init; }
    public DateTimeOffset CachedAt { get; init; }
}

public sealed class PlayerAdp
{
    public required PlayerId PlayerId { get; init; }
    public required string SourceKey { get; init; }
    public double OverallAdp { get; init; }
    public DateTimeOffset? SourceTimestamp { get; init; }
    public DateTimeOffset CachedAt { get; init; }
}

public sealed class PlayerProjection
{
    public required PlayerId PlayerId { get; init; }
    public required string SourceKey { get; init; }
    public double PassingAttempts { get; init; }
    public double Completions { get; init; }
    public double PassingYards { get; init; }
    public double PassingTouchdowns { get; init; }
    public double Interceptions { get; init; }
    public double RushingAttempts { get; init; }
    public double RushingYards { get; init; }
    public double RushingTouchdowns { get; init; }
    public double Targets { get; init; }
    public double Receptions { get; init; }
    public double ReceivingYards { get; init; }
    public double ReceivingTouchdowns { get; init; }
    public DateTimeOffset? SourceTimestamp { get; init; }
    public DateTimeOffset CachedAt { get; init; }
}

public sealed class PlayerProviderId
{
    public required PlayerId PlayerId { get; init; }
    public required string ProviderKey { get; init; }
    public required string ExternalId { get; init; }
}

public sealed class FantasyDataRefreshInfo
{
    public required string ProviderKey { get; init; }
    public required string Dataset { get; init; }
    public DateTimeOffset RefreshedAt { get; init; }
    public int RecordCount { get; init; }
}
