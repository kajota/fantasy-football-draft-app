using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Engine;

public static class RosterRules
{
    public static int QbDemand(IReadOnlyList<RosterSlot> slots)
    {
        return slots
            .Where(s => s.SlotKind is SlotKind.Required or SlotKind.Flex)
            .Where(s => s.EligiblePositions.Contains(PlayerPosition.QB))
            .Sum(s => s.Count);
    }

    public static int StartingSlotCount(IReadOnlyList<RosterSlot> slots) =>
        slots.Where(s => s.SlotKind is SlotKind.Required or SlotKind.Flex).Sum(s => s.Count);

    public static int TotalRosterSpots(IReadOnlyList<RosterSlot> slots) =>
        slots.Sum(s => s.Count);

    public static int DraftedRosterSpots(IReadOnlyList<RosterSlotSpecPreset> slots) =>
        slots.Where(s => s.SlotKind != SlotKind.Inactive).Sum(s => s.Count);

    public static bool IsSuperflexOrMultiQb(IReadOnlyList<RosterSlot> slots) =>
        QbDemand(slots) >= 2;

    public static Dictionary<PlayerPosition, int> RequiredStartingCounts(IReadOnlyList<RosterSlot> slots)
    {
        var counts = new Dictionary<PlayerPosition, int>();
        foreach (var slot in slots.Where(s => s.SlotKind == SlotKind.Required))
        {
            if (slot.EligiblePositions.Count != 1)
                continue;
            var position = slot.EligiblePositions[0];
            counts[position] = counts.GetValueOrDefault(position) + slot.Count;
        }

        return counts;
    }

    public static Dictionary<PlayerPosition, int> RemainingNeeds(
        IReadOnlyList<RosterSlot> slots,
        IReadOnlyList<PlayerPosition> draftedPositions)
    {
        var remaining = new Dictionary<PlayerPosition, int>();
        var assigned = draftedPositions.ToList();

        foreach (var slot in slots
                     .Where(s => s.SlotKind == SlotKind.Required)
                     .OrderBy(s => s.EligiblePositions.Count))
        {
            for (var i = 0; i < slot.Count; i++)
            {
                var match = assigned.FindIndex(p => slot.EligiblePositions.Contains(p));
                if (match >= 0)
                {
                    assigned.RemoveAt(match);
                    continue;
                }

                foreach (var position in slot.EligiblePositions)
                    remaining[position] = remaining.GetValueOrDefault(position) + 1;
            }
        }

        foreach (var slot in slots.Where(s => s.SlotKind == SlotKind.Flex))
        {
            for (var i = 0; i < slot.Count; i++)
            {
                var match = assigned.FindIndex(p => slot.EligiblePositions.Contains(p));
                if (match >= 0)
                {
                    assigned.RemoveAt(match);
                    continue;
                }

                if (slot.EligiblePositions.Contains(PlayerPosition.QB))
                    remaining[PlayerPosition.QB] = remaining.GetValueOrDefault(PlayerPosition.QB) + 1;
            }
        }

        return remaining;
    }

    public static IReadOnlyList<YahooRosterSlot> YahooSlotCatalog { get; } =
    [
        new("QB", "Quarterback", "QB", SlotKind.Required, [PlayerPosition.QB], 4),
        new("WR", "Wide Receiver", "WR", SlotKind.Required, [PlayerPosition.WR], 6),
        new("RB", "Running Back", "RB", SlotKind.Required, [PlayerPosition.RB], 6),
        new("TE", "Tight End", "TE", SlotKind.Required, [PlayerPosition.TE], 4),
        new("W/R", "Wide Receiver / Running Back", "W/R", SlotKind.Flex, [PlayerPosition.WR, PlayerPosition.RB], 4),
        new("W/T", "Wide Receiver / Tight End", "W/T", SlotKind.Flex, [PlayerPosition.WR, PlayerPosition.TE], 4),
        new("W/R/T", "Flex (WR / RB / TE)", "W/R/T", SlotKind.Flex, [PlayerPosition.WR, PlayerPosition.RB, PlayerPosition.TE], 4),
        new("Q/W/R/T", "Superflex (QB / WR / RB / TE)", "Q/W/R/T", SlotKind.Flex, [PlayerPosition.QB, PlayerPosition.WR, PlayerPosition.RB, PlayerPosition.TE], 4),
        new("K", "Kicker", "K", SlotKind.Required, [PlayerPosition.K], 3),
        new("DEF", "Defense / Special Teams", "DEF", SlotKind.Required, [PlayerPosition.DEF], 3),
        new("BN", "Bench", "BN", SlotKind.Bench, [PlayerPosition.QB, PlayerPosition.RB, PlayerPosition.WR, PlayerPosition.TE, PlayerPosition.K, PlayerPosition.DEF], 15),
        new("IR", "Injured Reserve", "IR", SlotKind.Inactive, [PlayerPosition.QB, PlayerPosition.RB, PlayerPosition.WR, PlayerPosition.TE, PlayerPosition.K, PlayerPosition.DEF], 6)
    ];

    public static IReadOnlyList<RosterSlotSpecPreset> DefaultStandardRoster() =>
        FromCounts(new Dictionary<string, int>
        {
            ["QB"] = 1, ["WR"] = 2, ["RB"] = 2, ["TE"] = 1, ["W/R/T"] = 1, ["K"] = 1, ["DEF"] = 1, ["BN"] = 6
        });

    public static IReadOnlyList<RosterSlotSpecPreset> DefaultYahooRoster() =>
        FromCounts(new Dictionary<string, int>
        {
            ["QB"] = 1, ["WR"] = 2, ["RB"] = 2, ["TE"] = 1, ["W/R/T"] = 1, ["K"] = 1, ["DEF"] = 1, ["BN"] = 6, ["IR"] = 2
        });

    public static IReadOnlyList<RosterSlotSpecPreset> DefaultSuperflexRoster() =>
        FromCounts(new Dictionary<string, int>
        {
            ["QB"] = 1, ["WR"] = 2, ["RB"] = 2, ["TE"] = 1, ["W/R/T"] = 1, ["Q/W/R/T"] = 1, ["K"] = 1, ["DEF"] = 1, ["BN"] = 6
        });

    public static IReadOnlyList<RosterSlotSpecPreset> Preset(bool superflex) =>
        superflex ? DefaultSuperflexRoster() : DefaultStandardRoster();

    public static IReadOnlyList<RosterSlotSpecPreset> FromCounts(IReadOnlyDictionary<string, int> counts)
    {
        return YahooSlotCatalog
            .Select(slot => new RosterSlotSpecPreset(slot.SlotCode, slot.SlotKind, counts.GetValueOrDefault(slot.SlotCode), slot.EligiblePositions))
            .Where(slot => slot.Count > 0)
            .ToList();
    }

    public static string CanonicalSlotCode(string slotCode, IReadOnlyList<PlayerPosition>? eligible = null)
    {
        return slotCode.Trim().ToUpperInvariant() switch
        {
            "FLEX" when eligible is not null && eligible.Contains(PlayerPosition.QB) => "Q/W/R/T",
            "FLEX" or "WR/RB/TE" => "W/R/T",
            "SUPERFLEX" or "SFLEX" or "Q/W/R/T" => "Q/W/R/T",
            "BENCH" or "BN" or "BE" => "BN",
            "DST" or "D/ST" or "DEF" => "DEF",
            "W/R/T" or "WR/RB/TE" => "W/R/T",
            "W/R" or "WR/RB" => "W/R",
            "W/T" or "WR/TE" => "W/T",
            var code => code
        };
    }

    public static Dictionary<string, int> CountsFromSlots(IEnumerable<RosterSlot> slots)
    {
        var counts = YahooSlotCatalog.ToDictionary(s => s.SlotCode, _ => 0);
        foreach (var slot in slots)
        {
            var code = CanonicalSlotCode(slot.SlotCode, slot.EligiblePositions);
            if (counts.ContainsKey(code))
                counts[code] += slot.Count;
        }

        return counts;
    }

    public static IReadOnlyList<ScoringPreset> DefaultScoring() =>
    [
        new(ScoringCategory.PassingYard, 0.04m),
        new(ScoringCategory.PassingTouchdown, 4m),
        new(ScoringCategory.Interception, -2m),
        new(ScoringCategory.RushingYard, 0.10m),
        new(ScoringCategory.RushingTouchdown, 6m),
        new(ScoringCategory.Reception, 0.50m),
        new(ScoringCategory.ReceivingYard, 0.10m),
        new(ScoringCategory.ReceivingTouchdown, 6m),
        new(ScoringCategory.FumbleLost, -2m),
        new(ScoringCategory.TwoPointConversion, 2m)
    ];
}

public sealed record RosterSlotSpecPreset(
    string SlotCode,
    SlotKind SlotKind,
    int Count,
    IReadOnlyList<PlayerPosition> EligiblePositions);

public sealed record YahooRosterSlot(
    string SlotCode,
    string DisplayName,
    string YahooName,
    SlotKind SlotKind,
    IReadOnlyList<PlayerPosition> EligiblePositions,
    int MaxCount);

public sealed record ScoringPreset(ScoringCategory Category, decimal Points);
