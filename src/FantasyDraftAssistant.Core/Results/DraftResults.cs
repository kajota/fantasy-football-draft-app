using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Results;

public sealed class DraftCommitResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public DraftId? DraftId { get; init; }
    public BranchId? BranchId { get; init; }
    public int StateVersion { get; init; }
    public IReadOnlyList<DraftEventRecord> Events { get; init; } = [];
    public DraftWorkingState? State { get; init; }

    public static DraftCommitResult Fail(string error) => new()
    {
        Succeeded = false,
        Error = error
    };

    public static DraftCommitResult Ok(DraftWorkingState state, IReadOnlyList<DraftEventRecord> events) => new()
    {
        Succeeded = true,
        DraftId = state.Draft.DraftId,
        BranchId = state.ActiveBranch.BranchId,
        StateVersion = state.Draft.CurrentStateVersion,
        Events = events,
        State = state
    };
}

public sealed class ValidationResult
{
    public required bool IsValid { get; init; }
    public string? Error { get; init; }

    public static ValidationResult Ok() => new() { IsValid = true };
    public static ValidationResult Fail(string error) => new() { IsValid = false, Error = error };
}

public sealed class DraftWorkingState
{
    public required League League { get; init; }
    public required Draft Draft { get; init; }
    public required DraftBranch ActiveBranch { get; set; }
    public required IReadOnlyList<Team> Teams { get; init; }
    public required IReadOnlyList<RosterSlot> RosterSlots { get; init; }
    public required IReadOnlyList<ScoringRule> ScoringRules { get; init; }
    public required IReadOnlyList<DraftSlot> Slots { get; init; }
    public required IReadOnlyList<Keeper> Keepers { get; set; }
    public Dictionary<int, ActiveSelection> ActiveSelections { get; } = new();
    public HashSet<PlayerId> UnavailablePlayers { get; } = [];
    public HashSet<PlayerId> KnownPlayers { get; } = [];
    public List<DraftQueueItem> Queue { get; } = [];
    public RedoCandidate? Redo { get; set; }
    public int NextSequenceNumber { get; set; } = 1;
    public List<DraftEventRecord> NewEvents { get; } = [];
    public DraftBranch? CreatedBranch { get; set; }

    public DraftSlot? CurrentSlot =>
        Slots.OrderBy(s => s.OverallPick).FirstOrDefault(s => !ActiveSelections.ContainsKey(s.OverallPick));

    public int CurrentOverallPick => CurrentSlot?.OverallPick ?? Slots.Count + 1;

    public ActiveSelection? SelectionAt(int overallPick) =>
        ActiveSelections.GetValueOrDefault(overallPick);

    public IReadOnlyList<ActiveSelection> SelectionsForTeam(TeamId teamId) =>
        ActiveSelections.Values.Where(s => s.TeamId == teamId).OrderBy(s => s.OverallPick).ToList();

    public DraftWorkingState CloneForBranch(DraftBranch branch)
    {
        var clone = new DraftWorkingState
        {
            League = League,
            Draft = new Draft
            {
                DraftId = Draft.DraftId,
                LeagueId = Draft.LeagueId,
                Name = Draft.Name,
                Season = Draft.Season,
                Status = Draft.Status,
                ActiveBranchId = branch.BranchId,
                CurrentStateVersion = Draft.CurrentStateVersion,
                SourceMode = Draft.SourceMode,
                CreatedAt = Draft.CreatedAt,
                StartedAt = Draft.StartedAt,
                CompletedAt = Draft.CompletedAt
            },
            ActiveBranch = branch,
            Teams = Teams,
            RosterSlots = RosterSlots,
            ScoringRules = ScoringRules,
            Slots = Slots,
            Keepers = Keepers,
            NextSequenceNumber = NextSequenceNumber,
            Redo = null
        };

        foreach (var selection in ActiveSelections.Values)
        {
            clone.ActiveSelections[selection.OverallPick] = selection;
            clone.UnavailablePlayers.Add(selection.PlayerId);
        }

        foreach (var item in Queue)
            clone.Queue.Add(item);

        return clone;
    }
}

public sealed class FantasyDataRefreshResult
{
    public required bool Succeeded { get; init; }
    public string? Error { get; init; }
    public int PlayersWritten { get; init; }
    public int RankingsWritten { get; init; }
    public int AdpWritten { get; init; }
    public int ProjectionsWritten { get; init; }
    public DateTimeOffset RefreshedAt { get; init; } = DateTimeOffset.UtcNow;

    public static FantasyDataRefreshResult Fail(string error) => new()
    {
        Succeeded = false,
        Error = error
    };
}

public sealed class FantasyDataRefreshRequest
{
    public int Season { get; init; } = DateTime.UtcNow.Year;
    public ConsensusScoring? Scoring { get; init; }
    public bool? Superflex { get; init; }
}

public sealed class AiConnectionTestResult
{
    public required bool Succeeded { get; init; }
    public string? Message { get; init; }
    public string? Model { get; init; }
}

public sealed class AiResponseChunk
{
    public string? Text { get; init; }
    public bool IsComplete { get; init; }
    public bool IsStale { get; init; }
    public int AnalyzedStateVersion { get; init; }
    public string? Error { get; init; }
    public int? InputTokens { get; init; }
    public int? OutputTokens { get; init; }
    public TimeSpan? Latency { get; init; }
}

public sealed class AiAnalysisRequest
{
    public required DraftId DraftId { get; init; }
    public required BranchId BranchId { get; init; }
    public required int StateVersion { get; init; }
    public required string Prompt { get; init; }
    public required bool FastMode { get; init; }
    public string? DecisionContextJson { get; init; }
    public string? Model { get; init; }
    public string? PromptKind { get; init; }
    public string? TauntStyle { get; init; }
    public string? TauntTarget { get; init; }

    // Prior advice turns for this provider, oldest first. Manual asks only;
    // the current decision context always outranks this history.
    public IReadOnlyList<AiConversationExchange> RecentTurns { get; init; } = [];
}

public sealed class AiConversationExchange
{
    public required string Question { get; init; }
    public required string Answer { get; init; }
    public int StateVersion { get; init; }
}

/// Result of a one-shot, non-streaming AI completion (see IAiProviderAdapter.CompleteTextAsync).
public sealed class AiTextCompletion
{
    public bool Succeeded { get; init; }
    public string Text { get; init; } = string.Empty;
    public string? Error { get; init; }
    public string? Model { get; init; }
}

public sealed class AiProviderConfig
{
    public required string ProviderKey { get; init; }
    public bool Enabled { get; set; }
    public required string Model { get; set; }
    public string Role { get; set; } = "Fast Advisor";
    public decimal? PerDraftSpendLimit { get; set; }
    public decimal? PerSessionSpendLimit { get; set; }
}

public sealed class ReadinessItem
{
    public required string Name { get; init; }
    public required ReadinessLevel Level { get; init; }
    public required string Detail { get; init; }
    public bool IsCritical { get; init; }
}

public sealed class DraftAlert
{
    public required AlertKind Kind { get; init; }
    public required string Message { get; init; }
    public int Severity { get; init; }
}
