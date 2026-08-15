using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class FantasyDataFormatTests
{
    [Fact]
    public void Maps_half_ppr_one_qb()
    {
        var format = FantasyDataFormat.FromLeague(
            Rules(0.5m),
            Slots(RosterRules.DefaultStandardRoster()));

        Assert.Equal(ConsensusScoring.HalfPpr, format.Scoring);
        Assert.False(format.Superflex);
        Assert.Equal("HALF", format.ScoringParam);
        Assert.Equal("ALL", format.PositionParam);
        Assert.Equal("fantasypros-half", format.SourceKey);
        Assert.Equal("Half PPR 1-QB", format.DisplayName);
    }

    [Fact]
    public void Maps_ppr_superflex()
    {
        var format = FantasyDataFormat.FromLeague(
            Rules(1m),
            Slots(RosterRules.DefaultSuperflexRoster()));

        Assert.Equal(ConsensusScoring.Ppr, format.Scoring);
        Assert.True(format.Superflex);
        Assert.Equal("PPR", format.ScoringParam);
        Assert.Equal("OP", format.PositionParam);
        Assert.Equal("fantasypros-ppr-sf", format.SourceKey);
    }

    [Fact]
    public void Maps_standard_near_zero_reception()
    {
        var format = FantasyDataFormat.FromLeague(Rules(0m), Slots(RosterRules.DefaultStandardRoster()));
        Assert.Equal(ConsensusScoring.Standard, format.Scoring);
        Assert.Equal("fantasypros-std", format.SourceKey);
    }

    [Fact]
    public void Round_trips_source_key()
    {
        var original = new FantasyDataFormat(ConsensusScoring.HalfPpr, Superflex: true);
        var parsed = FantasyDataFormat.TryParseSourceKey(original.SourceKey);
        Assert.Equal(original, parsed);
    }

    private static IReadOnlyList<ScoringRule> Rules(decimal reception) =>
    [
        new()
        {
            ScoringRuleId = ScoringRuleId.New(),
            LeagueId = LeagueId.New(),
            Category = ScoringCategory.Reception,
            Points = reception
        }
    ];

    private static IReadOnlyList<RosterSlot> Slots(IReadOnlyList<RosterSlotSpecPreset> preset) =>
        preset.Select(spec => new RosterSlot
        {
            RosterSlotId = RosterSlotId.New(),
            LeagueId = LeagueId.New(),
            SlotCode = spec.SlotCode,
            SlotKind = spec.SlotKind,
            Count = spec.Count,
            EligiblePositions = spec.EligiblePositions
        }).ToList();
}
