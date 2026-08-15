using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class RosterBoardBuilderTests
{
    [Fact]
    public void Required_slots_fill_before_flex_and_bench()
    {
        var board = RosterBoardBuilder.Build(SuperflexSlots(),
        [
            Player("Allen", PlayerPosition.QB, 1),
            Player("Jeanty", PlayerPosition.RB, 2),
            Player("Chase", PlayerPosition.WR, 3),
            Player("Gibbs", PlayerPosition.RB, 4),
            Player("Nacua", PlayerPosition.WR, 5),
            Player("Bowers", PlayerPosition.TE, 6),
            Player("Harrison", PlayerPosition.WR, 7)
        ]);

        Assert.Equal("Allen", Slot(board, "QB").Player);
        Assert.Equal("Jeanty", First(board, "RB").Player);
        Assert.Equal("Gibbs", board.Slots.Where(s => s.SlotCode == "RB").Last().Player);
        Assert.Equal("Harrison", Slot(board, "W/R/T").Player);
        Assert.True(Slot(board, "Q/W/R/T").IsFilled is false);
        Assert.Contains(board.OpenNeeds, need => need.Contains("Q/W/R/T") || need == "Q/W/R/T");
        Assert.StartsWith("Need ", board.NeedsLine);
        Assert.All(board.Slots.Where(s => s.SlotCode == "IR"), slot => Assert.False(slot.IsFilled));
    }

    [Fact]
    public void Second_qb_lands_in_superflex()
    {
        var board = RosterBoardBuilder.Build(SuperflexSlots(),
        [
            Player("Allen", PlayerPosition.QB, 1),
            Player("Lamar", PlayerPosition.QB, 2)
        ]);

        Assert.Equal("Allen", Slot(board, "QB").Player);
        Assert.Equal("Lamar", Slot(board, "Q/W/R/T").Player);
    }

    [Fact]
    public void Empty_board_lists_starter_needs()
    {
        var board = RosterBoardBuilder.Build(SuperflexSlots(), []);
        Assert.Contains("QB", board.OpenNeeds);
        Assert.Contains(board.Slots, slot => slot.SlotCode == "BN" && !slot.IsFilled);
        Assert.Equal(0, board.Slots.Count(slot => slot.IsFilled));
    }

    private static RosterBoardSlot Slot(RosterBoard board, string code) =>
        board.Slots.First(slot => slot.SlotCode == code);

    private static RosterBoardSlot First(RosterBoard board, string code) =>
        board.Slots.First(slot => slot.SlotCode == code);

    private static RosterBoardPlayer Player(string name, PlayerPosition position, int overall) =>
        new(name, position, "NFL", $"1.{overall:00}", overall);

    private static IReadOnlyList<RosterSlot> SuperflexSlots() =>
        RosterRules.DefaultSuperflexRoster().Select(spec => new RosterSlot
        {
            RosterSlotId = RosterSlotId.New(),
            LeagueId = LeagueId.New(),
            SlotCode = spec.SlotCode,
            SlotKind = spec.SlotKind,
            Count = spec.Count,
            EligiblePositions = spec.EligiblePositions
        }).ToList();
}
