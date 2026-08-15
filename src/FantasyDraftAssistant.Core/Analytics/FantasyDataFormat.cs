using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Analytics;

public enum ConsensusScoring
{
    Standard,
    HalfPpr,
    Ppr
}

public sealed record FantasyDataFormat(ConsensusScoring Scoring, bool Superflex)
{
    public static FantasyDataFormat Default { get; } = new(ConsensusScoring.HalfPpr, Superflex: false);

    public string ScoringParam => Scoring switch
    {
        ConsensusScoring.Standard => "STD",
        ConsensusScoring.Ppr => "PPR",
        _ => "HALF"
    };

    public string PositionParam => Superflex ? "OP" : "ALL";

    public string SourceKey => Superflex
        ? $"fantasypros-{ScoringParam.ToLowerInvariant()}-sf"
        : $"fantasypros-{ScoringParam.ToLowerInvariant()}";

    public string DisplayName => Superflex
        ? $"{ScoringLabel} Superflex"
        : $"{ScoringLabel} 1-QB";

    public string ScoringLabel => Scoring switch
    {
        ConsensusScoring.Standard => "Standard",
        ConsensusScoring.Ppr => "PPR",
        _ => "Half PPR"
    };

    public static FantasyDataFormat FromLeague(
        IReadOnlyList<ScoringRule> scoring,
        IReadOnlyList<RosterSlot> roster)
    {
        var reception = scoring
            .FirstOrDefault(rule => rule.Category == ScoringCategory.Reception)
            ?.Points ?? 0.5m;
        var bucket = reception switch
        {
            < 0.25m => ConsensusScoring.Standard,
            < 0.75m => ConsensusScoring.HalfPpr,
            _ => ConsensusScoring.Ppr
        };
        return new FantasyDataFormat(bucket, RosterRules.IsSuperflexOrMultiQb(roster));
    }

    public static FantasyDataFormat? TryParseSourceKey(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return null;
        var parts = sourceKey.Trim().ToLowerInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] != "fantasypros")
            return parts is ["fantasypros"] ? Default : null;

        var scoring = parts[1] switch
        {
            "std" => ConsensusScoring.Standard,
            "ppr" => ConsensusScoring.Ppr,
            "half" => ConsensusScoring.HalfPpr,
            _ => (ConsensusScoring?)null
        };
        if (scoring is null)
            return null;
        var superflex = parts.Contains("sf");
        return new FantasyDataFormat(scoring.Value, superflex);
    }
}
