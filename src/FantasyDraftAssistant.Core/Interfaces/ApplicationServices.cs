using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Core.Interfaces;

public interface IMockDraftService
{
    Task<IReadOnlyList<MockSeatPolicy>> GetPoliciesAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default);
    Task SeedPoliciesAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default);
    Task<MockPickResult> StartPracticeAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<MockPickResult> ReturnToLiveAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<MockPickResult> SimulateNextAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guesses what each seat between now and the user's next pick will take. A heuristic from
    /// the practice-draft policy, not a forecast of real managers. Changes nothing.
    /// </summary>
    Task<IReadOnlyList<PredictedPick>> PredictUpcomingPicksAsync(
        DraftId draftId,
        BranchId? branchId = null,
        CancellationToken cancellationToken = default);

    /// Why each AI seat took what it took, keyed by overall pick.
    Task<IReadOnlyList<MockPickReason>> GetPickReasonsAsync(
        DraftId draftId,
        BranchId branchId,
        CancellationToken cancellationToken = default);
}

public interface IDraftCommandService
{
    Task<DraftCommitResult> StartDraftAsync(StartDraftCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> DraftPlayerAsync(DraftPlayerCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> RollbackAsync(RollbackDraftCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> RedoAsync(RedoDraftCommand command, CancellationToken cancellationToken = default);
    Task<DraftCommitResult> ResetAsync(ResetDraftCommand command, CancellationToken cancellationToken = default);
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
    Task<IReadOnlyList<LeagueSummary>> ListArchivedLeaguesAsync(CancellationToken cancellationToken = default);
    Task ArchiveLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task RestoreLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task DeleteLeaguePermanentlyAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task<League> CreateLeagueAsync(CreateLeagueRequest request, CancellationToken cancellationToken = default);
    Task SaveLeagueDetailsAsync(LeagueId leagueId, string name, int season, int roundCount, string? draftGuidelines = null, CancellationToken cancellationToken = default);
    Task SaveBoardPublishAsync(LeagueId leagueId, string? boardSlug, bool publishBoard, CancellationToken cancellationToken = default);
    Task<League?> GetLeagueAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Team>> GetTeamsAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task SaveTeamsAsync(SaveTeamsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RosterSlot>> GetRosterSlotsAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task SaveRosterAsync(SaveRosterRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ScoringRule>> GetScoringRulesAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task SaveScoringAsync(SaveScoringRequest request, CancellationToken cancellationToken = default);
    Task SaveDraftOrderAsync(SaveDraftOrderRequest request, CancellationToken cancellationToken = default);
    Task<Draft> CreateDraftAsync(CreateDraftRequest request, CancellationToken cancellationToken = default);
    Task SaveKeepersAsync(SaveKeepersRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Keeper>> GetKeepersAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Draft>> ListDraftsAsync(LeagueId leagueId, CancellationToken cancellationToken = default);
    Task<Draft?> GetDraftAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DraftBranch>> GetBranchesAsync(DraftId draftId, CancellationToken cancellationToken = default);
    Task<League?> FindByExternalIdAsync(FantasyPlatform platform, string externalLeagueId, CancellationToken cancellationToken = default);
    Task<League> UpsertImportedLeagueAsync(ImportedLeagueRequest request, CancellationToken cancellationToken = default);
}

public interface IAnalyticsService
{
    Task<Analytics.AnalyticsSnapshot> GetSnapshotAsync(
        DraftId draftId,
        BranchId? branchId = null,
        CancellationToken cancellationToken = default,
        string? sourceKey = null);
}

public interface IDraftQueryService
{
    Task<LeagueSettingsDto> GetLeagueSettingsAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<DraftStatusDto> GetDraftStatusAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<RosterDto> GetMyRosterAsync(QueryContext context, CancellationToken cancellationToken = default);
    Task<RosterDto> GetTeamRosterAsync(QueryContext context, TeamId teamId, CancellationToken cancellationToken = default);
    Task<PlayerListDto> GetAvailablePlayersAsync(QueryContext context, PlayerFilter filter, CancellationToken cancellationToken = default);
    Task<RemainingPlayersSnapshot> GetRemainingPlayersAsync(QueryContext context, CancellationToken cancellationToken = default);
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
    string DisplayName { get; }
    string Description { get; }
    Task<FantasyDataRefreshResult> RefreshAsync(FantasyDataRefreshRequest request, CancellationToken cancellationToken);
}

public interface IFantasyDataProviderRegistry
{
    IReadOnlyList<IFantasyDataProvider> All { get; }
    IFantasyDataProvider? Get(string providerKey);
    Task<FantasyDataRefreshResult> RefreshPreferredAsync(FantasyDataRefreshRequest request, CancellationToken cancellationToken);
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
    Task<IReadOnlyList<string>> GetSourceKeysAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<PlayerId, IReadOnlyDictionary<string, string>>> GetProviderIdsAsync(CancellationToken cancellationToken = default);
}

public interface IAiProviderAdapter
{
    string ProviderKey { get; }
    Task<AiConnectionTestResult> TestConnectionAsync(AiProviderConfig config, CancellationToken cancellationToken = default);
    IAsyncEnumerable<AiResponseChunk> StreamAnalysisAsync(AiAnalysisRequest request, CancellationToken cancellationToken = default);

    // One-shot, non-streaming completion that is not tied to a draft. Used by
    // league import, which runs before any draft exists, so it cannot go through
    // AiAnalysisRequest (that requires a DraftId/BranchId/StateVersion) and is
    // deliberately not recorded against per-draft AI spend.
    Task<AiTextCompletion> CompleteTextAsync(
        string? model,
        string systemPrompt,
        string userPrompt,
        int maxOutputTokens,
        CancellationToken cancellationToken = default);
}

public interface IAiProviderRegistry
{
    IReadOnlyList<IAiProviderAdapter> All { get; }
    IAiProviderAdapter? Get(string providerKey);
}

public interface IAppSettingsStore
{
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}

public interface IBoardPublisher
{
    string LastStatus { get; }
    event EventHandler? StatusChanged;
    void Schedule(DraftId draftId, BranchId branchId);
    Task PublishNowAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default);
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

public interface IYahooAuthService
{
    Task<YahooAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default);
    Task SaveAppCredentialsAsync(string clientId, string clientSecret, string? redirectUri, CancellationToken cancellationToken = default);
    Task ClearAppCredentialsAsync(CancellationToken cancellationToken = default);
    Task<YahooSignInStart> StartSignInAsync(CancellationToken cancellationToken = default);
    Task CompleteSignInAsync(string authorizationCode, string? state, CancellationToken cancellationToken = default);
    Task SignOutAsync(CancellationToken cancellationToken = default);
}

public interface IYahooLeagueImporter
{
    Task<IReadOnlyList<YahooLeagueListItem>> ListLeaguesAsync(CancellationToken cancellationToken = default);
    Task<YahooImportPreview> PreviewAsync(string leagueKey, CancellationToken cancellationToken = default);
    Task<YahooImportResult> ImportAsync(string leagueKey, YahooImportOptions options, CancellationToken cancellationToken = default);
}

/// Import path for a Yahoo league the API cannot reach — a private league with no
/// approved Yahoo developer application. Takes text the user pasted out of their
/// signed-in browser and produces the same result as <see cref="IYahooLeagueImporter"/>.
public interface IYahooPasteImporter
{
    /// Deterministic parse. Never calls out to a network.
    YahooPasteParseResult Parse(YahooPasteInput input);

    /// Providers that could read a paste — enabled and holding an API key. Empty
    /// means the AI fallback is unusable.
    Task<IReadOnlyList<YahooAiReaderOption>> ListAiReadersAsync(CancellationToken cancellationToken = default);

    /// Fallback for a paste the deterministic parser could not read. The caller
    /// names the provider so the choice, and the cost, is never implicit.
    Task<YahooPasteParseResult> ParseWithAiAsync(
        YahooPasteInput input,
        string providerKey,
        CancellationToken cancellationToken = default);

    YahooImportPreview Preview(YahooLeagueSnapshot snapshot);
    Task<YahooImportPreview> PreviewAsync(YahooLeagueSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<YahooImportResult> ImportAsync(YahooLeagueSnapshot snapshot, YahooImportOptions options, CancellationToken cancellationToken = default);
}

/// A provider that is enabled on AI Providers and holds an API key, so it can actually
/// be asked to do something. Shared by every feature that has to offer the user a
/// choice of model rather than picking one silently.
public sealed record AiModelChoice
{
    public required string ProviderKey { get; init; }
    public required string Model { get; init; }
    public required string DisplayName { get; init; }
    public string? Role { get; init; }

    /// "openai:gpt-5.6-luna" - the form stored on a seat.
    public string Key => $"{ProviderKey}:{Model}";

    public override string ToString() => $"{DisplayName} · {Model}";
}

public interface IAiModelOptions
{
    /// Fast Advisor first, so a default selection lands on the model meant for cheap,
    /// quick work rather than an expensive one.
    Task<IReadOnlyList<AiModelChoice>> ListEnabledAsync(CancellationToken cancellationToken = default);
}

/// Hands one practice-draft seat's pick to an AI model.
///
/// Only ever consulted for an actual pick. The turn outlook simulates dozens of picks
/// synchronously on every refresh and must never reach this - it uses the seat
/// strategy's deterministic proxy personality instead.
public interface IMockPickAdvisor
{
    /// Null means "could not decide" - no model configured, a timeout, a bad answer, a
    /// player that is not actually available. Callers fall back to MockPickPolicy.
    Task<MockAiPick?> ChooseAsync(MockAiPickRequest request, CancellationToken cancellationToken = default);
}

public sealed class MockAiPickRequest
{
    public required DraftId DraftId { get; init; }
    public required TeamId TeamId { get; init; }

    /// "provider:model", e.g. "openai:gpt-5.6-luna".
    public required string AiModel { get; init; }

    /// The seat's private brief, from MockAiStrategyCatalog.
    public required string StrategyPrompt { get; init; }

    public required int Round { get; init; }
    public required int RoundPick { get; init; }
    public required int RoundCount { get; init; }
    public required string TeamName { get; init; }

    /// The shortlist the model chooses from. Nothing outside it is a legal answer.
    public required IReadOnlyList<MockAiCandidate> Candidates { get; init; }

    public IReadOnlyList<string> RosterSoFar { get; init; } = [];
    public IReadOnlyList<string> RemainingNeeds { get; init; } = [];
    public IReadOnlyList<string> RecentPicks { get; init; } = [];
    public string? ScoringSummary { get; init; }
}

public sealed record MockAiCandidate(
    PlayerId PlayerId,
    string Name,
    string Position,
    string? Team,
    int OverallRank,
    double? Adp,
    int YearsExp);

public sealed class MockAiPick
{
    public required PlayerId PlayerId { get; init; }
    public required string Reason { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
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

public interface IAiResponseStore
{
    Task SaveAsync(AiSavedResponse response, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiSavedResponse>> ListAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default);
}

public sealed class AiSavedResponse
{
    public required string ResponseId { get; init; }
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
    public required string Provider { get; init; }
    public required string Model { get; init; }
    public required int AnalyzedStateVersion { get; init; }
    public required string Prompt { get; init; }
    public required string Body { get; init; }
    public required DateTimeOffset RequestStartedAt { get; init; }
    public DateTimeOffset? ResponseCompletedAt { get; init; }

    // Null or empty means a regular advice turn; taunt/watch turns carry
    // their prompt kind so conversation windows can skip them.
    public string? PromptKind { get; init; }
}
