using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Interfaces;

public interface IDraftCommandService
{
    Task<DraftCommitResult> StartDraftAsync(StartDraftCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> DraftPlayerAsync(DraftPlayerCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> RollbackAsync(RollbackDraftCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> RedoAsync(RedoDraftCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> CorrectPickAsync(CorrectPickCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> CreateBranchAsync(CreateDraftBranchCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> SwitchBranchAsync(SwitchDraftBranchCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> CompleteDraftAsync(CompleteDraftCommand command, CancellationToken cancellationToken = default);
}

public interface IDraftStateService
{
    Task<DraftWorkingState?> GetWorkingStateAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Player>> GetPlayersAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftQueueItem>> GetQueueAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default);
    Task AddToQueueAsync(QueueAddCommand command, CancellationToken cancellationToken = default);
    Task RemoveFromQueueAsync(QueueRemoveCommand command, CancellationToken cancellationToken = default);
    Task ReorderQueueAsync(QueueReorderCommand command, CancellationToken cancellationToken = default);
    Task<IntegrityReport> ValidateAsync(DraftId draftId, CancellationToken cancellationToken = default);
}

public interface ILeagueService
{
    Task<IReadOnlyList<LeagueSummary>> ListLeaguesAsync(CancellationToken cancellationToken = default);
    Task<League> CreateLeagueAsync(CreateLeagueRequest request, CancellationToken cancellationToken = default);
    Task SaveLeagueDetailsAsync(LeagueId leagueId, string name, int season, int roundCount, CancellationToken cancellationToken = default);
    Task<League?> GetLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetTeamsAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task SaveTeamsAsync(SaveTeamsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RosterSlot>> GetRosterSlotsAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task SaveRosterAsync(SaveRosterRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoringRule>> GetScoringRulesAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task SaveScoringAsync(SaveScoringRequest request, CancellationToken cancellationToken = default);
    Task<Draft> CreateDraftAsync(CreateDraftRequest request, CancellationToken cancellationToken = default);
    Task SaveKeepersAsync(SaveKeepersRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Keeper>> GetKeepersAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Draft>> ListDraftsAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task<Draft?> GetDraftAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftBranch>> GetBranchesAsync(DraftId draftId, CancellationToken cancellationToken = default);
}

public interface IAnalyticsService
{
    Task<Analytics.AnalyticsSnapshot> GetSnapshotAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default);
}

public interface IDraftQueryService
{
    Task<LeagueSettingsDto> GetLeagueSettingsAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<DraftStatusDto> GetDraftStatusAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<RosterDto> GetMyRosterAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<RosterDto> GetTeamRosterAsync(QueryContext context, TeamId teamId, CancellationToken cancellationToken = default);
    Task<PlayerListDto> GetAvailablePlayersAsync(QueryContext context, PlayerFilter filter, CancellationToken cancellationToken = default);
    Task<PlayerDetailsDto> GetPlayerDetailsAsync(QueryContext context, PlayerId playerId, CancellationToken cancellationToken = default);
    Task<RecentPicksDto> GetRecentPicksAsync(QueryContext context, int count, CancellationToken cancellationToken = default);
    Task<PositionSummaryDto> GetPositionSummaryAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<RemainingTiersDto> GetRemainingTiersAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<UpcomingTeamsDto> GetUpcomingTeamsAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<DraftBoardDto> GetDraftBoardAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<MyQueueDto> GetMyQueueAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<DecisionContextDto> GetDecisionContextAsync(QueryContext context, CancellationToken cancellationToken = default);
}

public interface IDraftChangeNotifier
{
    event EventHandler<DraftChangedEventArgs>? DraftChanged;
    void Notify(DraftId draftId, BranchId branchId, int stateVersion);
}

public sealed class DraftChangedEventArgs : EventArgs
{
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
    public required int StateVersion { get; init; }
}

public interface IDraftSource
{
    DraftSourceKind Kind { get; }
    IAsyncEnumerable<DraftSourceEvent> WatchAsync(DraftSourceContext context, CancellationToken cancellationToken);
}

public enum DraftSourceKind
{
    Manual,
    Yahoo
}

public sealed class DraftSourceContext
{
    public required DraftId DraftId { get; init; }
}

public sealed class DraftSourceEvent
{
    public required PlayerId PlayerId { get; init; }
    public required TeamId TeamId { get; init; }
    public required int OverallPick { get; init; }
    public required PickSource Source { get; init; }
    public string? ExternalSourceId { get; init; }
    public DateTimeOffset ObservedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IFantasyDataProvider
{
    string ProviderKey { get; }
    Task<FantasyDataRefreshResult> RefreshAsync(FantasyDataRefreshRequest request, CancellationToken cancellationToken);
}

public interface IFantasyDataWriter
{
    Task<FantasyDataRefreshResult> WriteAsync(
        string providerKey,
        IReadOnlyList<Player> players,
        IReadOnlyList<PlayerProviderId> providerIds,
        IReadOnlyList<PlayerRanking> rankings,
        IReadOnlyList<PlayerAdp> adp,
        IReadOnlyList<PlayerProjection> projections,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<PlayerId, PlayerRanking>> GetRankingsAsync(string? sourceKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<PlayerId, PlayerAdp>> GetAdpAsync(string? sourceKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<PlayerId, PlayerProjection>> GetProjectionsAsync(string? sourceKey = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FantasyDataRefreshInfo>> GetRefreshInfoAsync(CancellationToken cancellationToken = default);
}

public interface IAiProviderAdapter
{
    string ProviderKey { get; }
    Task<AiConnectionTestResult> TestConnectionAsync(AiProviderConfig config, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AiResponseChunk> StreamAnalysisAsync(AiAnalysisRequest request, CancellationToken cancellationToken = default);
}

public interface IAiProviderRegistry
{
    IReadOnlyList<IAiProviderAdapter> All { get; }
    IAiProviderAdapter? Get(string providerKey);
}

public interface ICredentialStore
{
    bool IsSecure { get; }
    string Description { get; }
    Task SaveSecretAsync(string scope, string key, string secret, CancellationToken cancellationToken = default);
    Task<string?> GetSecretAsync(string scope, string key, CancellationToken cancellationToken = default);
    Task DeleteSecretAsync(string scope, string key, CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<string> CreateBackupAsync(string reason, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BackupInfo>> ListBackupsAsync(CancellationToken cancellationToken = default);
}

public sealed class BackupInfo
{
    public required string Path { get; init; }
    public required string Reason { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

public interface IReadinessService
{
    Task<IReadOnlyList<ReadinessItem>> CheckAsync(DraftId? draftId, CancellationToken cancellationToken = default);
}

public interface IAiUsageService
{
    Task RecordAsync(AiUsageRecord record, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiUsageRecord>> ListForDraftAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<decimal> EstimatedDraftSpendAsync(DraftId draftId, string? providerKey = null, CancellationToken cancellationToken = default);
}

public sealed class AiUsageRecord
{
    public required DraftId DraftId { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public int AnalyzedStateVersion { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public decimal? EstimatedCost { get; init; }
    public TimeSpan? Latency { get; init; }
    public DateTimeOffset RequestStartedAt { get; init; }
    public DateTimeOffset? ResponseCompletedAt { get; init; }
}

public interface IAiConfigStore
{
    Task<IReadOnlyList<AiProviderConfig>> ListAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AiProviderConfig config, CancellationToken cancellationToken = default);
}
