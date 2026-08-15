using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Engine;

public sealed record RosterBoardPlayer(
    string Name,
    PlayerPosition Position,
    string NflTeam,
    string RoundPick,
    int OverallPick);

public sealed record RosterBoardSlot(
    string SlotCode,
    string DisplayName,
    SlotKind Kind,
    string? Player,
    string? Position,
    string? NflTeam,
    string? RoundPick,
    bool IsFilled);

public sealed record RosterBoard(
    IReadOnlyList<RosterBoardSlot> Slots,
    IReadOnlyList<string> OpenNeeds,
    string NeedsLine);

public static class RosterBoardBuilder
{
    public static RosterBoard Build(
        IReadOnlyList<RosterSlot> slots,
        IReadOnlyList<RosterBoardPlayer> drafted)
    {
        var remaining = drafted.OrderBy(player => player.OverallPick).ToList();
        var assigned = new List<RosterBoardSlot>();

        foreach (var open in Expand(slots).Where(slot => slot.Kind != SlotKind.Inactive))
        {
            var index = remaining.FindIndex(player => open.Eligible.Contains(player.Position));
            if (index >= 0)
            {
                var player = remaining[index];
                remaining.RemoveAt(index);
                assigned.Add(Filled(open, player));
            }
            else
            {
                assigned.Add(Empty(open));
            }
        }

        foreach (var open in Expand(slots).Where(slot => slot.Kind == SlotKind.Inactive))
            assigned.Add(Empty(open));

        foreach (var extra in remaining)
        {
            assigned.Add(new RosterBoardSlot(
                "EXTRA",
                "Extra",
                SlotKind.Bench,
                extra.Name,
                extra.Position.ToString(),
                extra.NflTeam,
                extra.RoundPick,
                true));
        }

        var needs = assigned
            .Where(slot => !slot.IsFilled && slot.Kind is SlotKind.Required or SlotKind.Flex)
            .GroupBy(slot => slot.SlotCode)
            .Select(group => group.Count() == 1 ? group.Key : $"{group.Count()} {group.Key}")
            .ToList();

        var line = needs.Count == 0
            ? "Starting lineup is full."
            : "Need " + string.Join(", ", needs);

        return new RosterBoard(assigned, needs, line);
    }

    private static IReadOnlyList<OpenSlot> Expand(IReadOnlyList<RosterSlot> slots)
    {
        return slots
            .SelectMany(slot => Enumerable.Range(0, Math.Max(0, slot.Count)).Select(_ => new OpenSlot(
                RosterRules.CanonicalSlotCode(slot.SlotCode, slot.EligiblePositions),
                DisplayName(slot),
                slot.SlotKind,
                slot.EligiblePositions)))
            .OrderBy(slot => KindOrder(slot.Kind))
            .ThenBy(slot => CatalogOrder(slot.SlotCode))
            .ToList();
    }

    private static string DisplayName(RosterSlot slot)
    {
        var code = RosterRules.CanonicalSlotCode(slot.SlotCode, slot.EligiblePositions);
        return RosterRules.YahooSlotCatalog.FirstOrDefault(item => item.SlotCode == code)?.DisplayName
               ?? slot.SlotCode;
    }

    private static int KindOrder(SlotKind kind) => kind switch
    {
        SlotKind.Required => 0,
        SlotKind.Flex => 1,
        SlotKind.Bench => 2,
        _ => 3
    };

    private static int CatalogOrder(string code)
    {
        var index = -1;
        for (var i = 0; i < RosterRules.YahooSlotCatalog.Count; i++)
        {
            if (RosterRules.YahooSlotCatalog[i].SlotCode == code)
            {
                index = i;
                break;
            }
        }

        return index < 0 ? 100 : index;
    }

    private static RosterBoardSlot Filled(OpenSlot slot, RosterBoardPlayer player) =>
        new(slot.SlotCode, slot.DisplayName, slot.Kind, player.Name, player.Position.ToString(), player.NflTeam, player.RoundPick, true);

    private static RosterBoardSlot Empty(OpenSlot slot) =>
        new(slot.SlotCode, slot.DisplayName, slot.Kind, null, null, null, null, false);

    private sealed record OpenSlot(
        string SlotCode,
        string DisplayName,
        SlotKind Kind,
        IReadOnlyList<PlayerPosition> Eligible);
}
