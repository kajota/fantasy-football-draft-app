using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Tests;

public class RosterRulesTests
{
    [Fact]
    public void Remaining_needs_fill_required_slots_before_flex()
    {
        var state = LeagueFactory.CreateStandardState(superflex: true);
        var drafted = new[] { PlayerPosition.QB, PlayerPosition.RB, PlayerPosition.WR };
        var remaining = RosterRules.RemainingNeeds(state.RosterSlots, drafted);

        Assert.Equal(1, remaining[PlayerPosition.RB]);
        Assert.Equal(1, remaining[PlayerPosition.WR]);
        Assert.Equal(1, remaining[PlayerPosition.TE]);
        Assert.True(remaining[PlayerPosition.QB] >= 1);
    }

    [Fact]
    public void Remaining_roster_needs_keep_flex_slots_explicit()
    {
        var state = LeagueFactory.CreateStandardState(superflex: false);
        var drafted = new[]
        {
            PlayerPosition.QB,
            PlayerPosition.RB,
            PlayerPosition.RB,
            PlayerPosition.WR,
            PlayerPosition.WR,
            PlayerPosition.TE,
            PlayerPosition.K,
            PlayerPosition.DEF
        };

        var needs = RosterRules.RemainingRosterNeeds(state.RosterSlots, drafted);

        var need = Assert.Single(needs);
        Assert.Equal("W/R/T", need.SlotCode);
        Assert.Equal(1, need.Count);
        Assert.Equal([PlayerPosition.WR, PlayerPosition.RB, PlayerPosition.TE], need.EligiblePositions);
    }

    [Fact]
    public void Standard_roster_is_one_qb_not_superflex()
    {
        var slots = RosterRules.DefaultStandardRoster();
        var mapped = slots.Select(s => new FantasyDraftAssistant.Core.Models.RosterSlot
        {
            RosterSlotId = FantasyDraftAssistant.Core.Ids.RosterSlotId.New(),
            LeagueId = FantasyDraftAssistant.Core.Ids.LeagueId.New(),
            SlotCode = s.SlotCode,
            SlotKind = s.SlotKind,
            Count = s.Count,
            EligiblePositions = s.EligiblePositions
        }).ToList();

        Assert.Equal(1, RosterRules.QbDemand(mapped));
        Assert.False(RosterRules.IsSuperflexOrMultiQb(mapped));
        Assert.Equal(15, RosterRules.TotalRosterSpots(mapped));
        Assert.DoesNotContain(slots, s => s.SlotCode == "SUPERFLEX");
        Assert.DoesNotContain(slots, s => s.SlotCode == "Q/W/R/T");
    }

    [Fact]
    public void Yahoo_catalog_covers_commissioner_slot_types()
    {
        var codes = RosterRules.YahooSlotCatalog.Select(s => s.SlotCode).ToList();
        foreach (var expected in new[] { "QB", "WR", "RB", "TE", "W/R", "W/T", "W/R/T", "Q/W/R/T", "K", "DEF", "BN", "IR" })
            Assert.Contains(expected, codes);
    }

    [Fact]
    public void Canonical_codes_map_legacy_and_yahoo_names()
    {
        Assert.Equal("W/R/T", RosterRules.CanonicalSlotCode("FLEX"));
        Assert.Equal("Q/W/R/T", RosterRules.CanonicalSlotCode("SUPERFLEX"));
        Assert.Equal("Q/W/R/T", RosterRules.CanonicalSlotCode("FLEX", [PlayerPosition.QB, PlayerPosition.RB]));
        Assert.Equal("BN", RosterRules.CanonicalSlotCode("BENCH"));
        Assert.Equal("DEF", RosterRules.CanonicalSlotCode("D/ST"));
    }

    [Fact]
    public void Yahoo_default_matches_public_league_roster()
    {
        var slots = RosterRules.DefaultYahooRoster();
        Assert.Equal(2, slots.Single(s => s.SlotCode == "IR").Count);
        Assert.Equal(15, RosterRules.DraftedRosterSpots(slots));
        Assert.Equal(1, slots.Single(s => s.SlotCode == "W/R/T").Count);
        Assert.DoesNotContain(slots, s => s.SlotCode == "Q/W/R/T");
    }

    [Fact]
    public void Drafted_roster_spots_exclude_ir()
    {
        var specs = new[]
        {
            new RosterSlotSpec
            {
                SlotCode = "QB",
                SlotKind = SlotKind.Required,
                Count = 1,
                EligiblePositions = [PlayerPosition.QB]
            },
            new RosterSlotSpec
            {
                SlotCode = "BN",
                SlotKind = SlotKind.Bench,
                Count = 6,
                EligiblePositions = [PlayerPosition.RB]
            },
            new RosterSlotSpec
            {
                SlotCode = "IR",
                SlotKind = SlotKind.Inactive,
                Count = 2,
                EligiblePositions = [PlayerPosition.RB]
            }
        };

        Assert.Equal(7, RosterRules.DraftedRosterSpots(specs));
    }
}
