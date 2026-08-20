using System.Text.Json.Serialization;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Query;

public sealed class QueryContext
{
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
}

public sealed class LeagueSettingsDto
{
    public required string Name { get; init; }
    public required int Season { get; init; }
    public string NflSeason => $"{Season} NFL season";
    public required int TeamCount { get; init; }
    public required string DraftType { get; init; }
    public required int RoundCount { get; init; }
    public required int RosterSize { get; init; }
    public required bool SuperflexOrMultiQb { get; init; }
    public required int QbDemand { get; init; }
    public required IReadOnlyList<string> RosterSlots { get; init; }
    public required IReadOnlyDictionary<string, decimal> Scoring { get; init; }
    public required string ScoringProfile { get; init; }
    public required IReadOnlyList<string> ScoringLines { get; init; }
    public string? DraftGuidelines { get; init; }
    public string? KeeperNote { get; init; }
}

public sealed class DraftStatusDto
{
    public required string Status { get; init; }
    public required string SourceMode { get; init; }
    public required int StateVersion { get; init; }
    public required string ActiveBranch { get; init; }
    public required int CurrentOverallPick { get; init; }
    public required string CurrentRoundPick { get; init; }
    public string? CurrentTeam { get; init; }
    public string? UserNextRoundPick { get; init; }
    public int? UserNextOverallPick { get; init; }
    public int PicksUntilUser { get; init; }
}

public sealed class RosterDto
{
    public required string TeamName { get; init; }
    public required IReadOnlyList<RosterPlayerDto> Players { get; init; }
}

public sealed class RosterPlayerDto
{
    public required string Name { get; init; }
    public required string Position { get; init; }
    public required string NflTeam { get; init; }
    public required string RoundPick { get; init; }
}

public sealed class PlayerListDto
{
    public required IReadOnlyList<PlayerSummaryDto> Players { get; init; }
}

public sealed class PlayerSummaryDto
{
    public required string PlayerId { get; init; }
    public required string Name { get; init; }
    public required string Position { get; init; }
    public required string NflTeam { get; init; }
    public int? ByeWeek { get; init; }
    public required string Status { get; init; }
    public int? OverallRank { get; init; }
    public int? PositionRank { get; init; }
    public int? Tier { get; init; }
    public int? RankMin { get; init; }
    public int? RankMax { get; init; }
    public double? RankStd { get; init; }
    public string? RankRange { get; init; }
    public double? OverallAdp { get; init; }
    public string? AdpRoundPick { get; init; }
    public decimal? ProjectedPoints { get; init; }
    public int? YearsExp { get; init; }
    public bool IsRookie { get; init; }
    public string? InjuryBodyPart { get; init; }
    public string? InjuryNotes { get; init; }
    public string? InjuryStartedOn { get; init; }
    public string? InjuryLine { get; init; }
    public string? HandcuffFor { get; init; }
    public string? SharedByeWith { get; init; }
    public int? SharedByeWeek { get; init; }

    // Decision-context annotations, set after mapping. Mutable on purpose.
    public decimal? PointsAboveReplacement { get; set; }
    public string? NextPickOutlook { get; set; }

    /// <summary>Chance, 0-100, the player is drafted before the user's next pick.</summary>
    public int? NextPickGonePercent { get; set; }
}

public sealed class PlayerDetailsDto
{
    public required PlayerSummaryDto Summary { get; init; }
    public DateTimeOffset? StatusUpdatedAt { get; init; }
}

public sealed class RecentPicksDto
{
    public required IReadOnlyList<PickDto> Picks { get; init; }
}

public sealed class PickDto
{
    public required int OverallPick { get; init; }
    public required string RoundPick { get; init; }
    public required string Team { get; init; }
    public string TeamId { get; init; } = "";
    public required string Player { get; init; }
    public required string Position { get; init; }
    public required string NflTeam { get; init; }
    public required string Source { get; init; }
}

public sealed class PositionSummaryDto
{
    public required IReadOnlyDictionary<string, int> Drafted { get; init; }
    public required IReadOnlyDictionary<string, int> Available { get; init; }
}

public sealed class RemainingTiersDto
{
    public required IReadOnlyDictionary<int, int> RemainingByTier { get; init; }
}

public sealed class UpcomingTeamsDto
{
    public required IReadOnlyList<string> Teams { get; init; }
}

public sealed class DraftBoardDto
{
    public required IReadOnlyList<PickDto> Picks { get; init; }
}

public sealed class MyQueueDto
{
    public required IReadOnlyList<PlayerSummaryDto> Players { get; init; }
}

public sealed class UpcomingPickDto
{
    public required int OverallPick { get; init; }
    public required string RoundPick { get; init; }
    public required string Team { get; init; }
    public string TeamId { get; init; } = "";
    public bool IsUser { get; init; }
}

public sealed class InterveningTeamDto
{
    public required string TeamName { get; init; }
    public string TeamId { get; init; } = "";
    public required int PicksBeforeUser { get; init; }

    // Full roster when it is still small; otherwise position counts plus
    // the team's latest picks, to keep the AI context lean late in drafts.
    public IReadOnlyList<RosterPlayerDto>? Roster { get; init; }
    public IReadOnlyDictionary<string, int>? RosterPositionCounts { get; init; }
    public IReadOnlyList<string> RecentAdditions { get; init; } = [];
    public required IReadOnlyList<string> RemainingNeeds { get; init; }
}

public sealed class DataFreshnessDto
{
    public required IReadOnlyList<DataFreshnessItemDto> Sources { get; init; }
}

public sealed class DataFreshnessItemDto
{
    public required string ProviderKey { get; init; }
    public required string Dataset { get; init; }
    public required string RefreshedAt { get; init; }
    public required string Age { get; init; }
    public int RecordCount { get; init; }
}

public sealed class DecisionContextDto
{
    public required DraftStatusDto Status { get; init; }
    public required LeagueSettingsDto League { get; init; }
    public required RosterDto MyRoster { get; init; }
    public required IReadOnlyList<string> MyRemainingNeeds { get; init; }
    public required MyQueueDto Queue { get; init; }
    public required IReadOnlyList<PlayerSummaryDto> TopAvailable { get; init; }
    public required PositionSummaryDto Positions { get; init; }
    public required RemainingTiersDto Tiers { get; init; }
    public required IReadOnlyList<string> RecentPositions { get; init; }
    public required IReadOnlyList<string> AllTeamNeeds { get; init; }
    public required IReadOnlyList<string> Alerts { get; init; }
    public required string RankingsSource { get; init; }
    public required IReadOnlyList<PlayerSummaryDto> AvailableRookies { get; init; }
    public required IReadOnlyList<PlayerSummaryDto> InjuredAvailable { get; init; }
    public required int StateVersion { get; init; }

    // Additive live-draft context. Defaults keep older callers compiling.
    public IReadOnlyList<PickDto> RecentPicks { get; init; } = [];
    public IReadOnlyList<UpcomingPickDto> UpcomingPicks { get; init; } = [];
    public IReadOnlyList<UpcomingPickDto> MyUpcomingPicks { get; init; } = [];
    public IReadOnlyList<InterveningTeamDto> InterveningTeams { get; init; } = [];
    public RosterDto? CurrentTeamRoster { get; init; }
    public DataFreshnessDto? DataFreshness { get; init; }
    public IReadOnlyList<string> TierCliffs { get; init; } = [];

    /// <summary>
    /// The same cliffs structured for the UI. Ignored when serialising so the AI payload
    /// keeps carrying only the prose form.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<TierCliff> TierCliffDetail { get; init; } = [];
    public IReadOnlyDictionary<string, int> PositionThreats { get; init; } =
        new Dictionary<string, int>();
    public IReadOnlyList<string> MyByeWeeks { get; init; } = [];
    public string? GeneratedAt { get; init; }
}

public enum PlayerListSort
{
    Rank = 0,
    Name = 1,
    Position = 2,
    NflTeam = 3,
    Adp = 4,
    ProjectedPoints = 5
}

public sealed class PlayerFilter
{
    public PlayerPosition? Position { get; init; }
    public int? MaxResults { get; init; } = 40;
    public string? Search { get; init; }
    public PlayerListSort SortBy { get; init; } = PlayerListSort.Rank;
    public bool SortDescending { get; init; }
    public string? SourceKey { get; init; }
}

public sealed class LeagueSummary
{
    public required LeagueId LeagueId { get; init; }
    public required string Name { get; init; }
    public required int Season { get; init; }
    public required int TeamCount { get; init; }
    public required DraftType DraftType { get; init; }
    public FantasyPlatform Platform { get; init; } = FantasyPlatform.Manual;
    public DraftId? ActiveDraftId { get; init; }
    public DraftStatus? ActiveDraftStatus { get; init; }
    public int DraftCount { get; init; }
    public DateTimeOffset? ArchivedAt { get; init; }
    public bool IsArchived => ArchivedAt is not null;
    public bool IsYahoo => Platform == FantasyPlatform.Yahoo;
    public string DetailLine => ActiveDraftStatus is { } status
        ? $"{TeamCount} teams · Season {Season} · {status}"
        : $"{TeamCount} teams · Season {Season}";
    public string ArchivedDetailLine => $"Archived · {DetailLine}";
}

public sealed class IntegrityReport
{
    public required bool IsHealthy { get; init; }
    public IReadOnlyList<string> Issues { get; init; } = [];
}
