using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class DraftGuidelinePresetsTests
{
    [Fact]
    public void House_rules_cover_kicker_defense_and_backups()
    {
        Assert.Contains("K or DEF", DraftGuidelinePresets.HouseRules.Body, StringComparison.Ordinal);
        Assert.Contains("backup QB", DraftGuidelinePresets.HouseRules.Body, StringComparison.Ordinal);
        Assert.Contains("backup TE", DraftGuidelinePresets.HouseRules.Body, StringComparison.Ordinal);
    }

    [Fact]
    public void Strategy_presets_are_distinct_and_named()
    {
        Assert.Equal(9, DraftGuidelinePresets.All.Count);
        Assert.Equal(DraftGuidelinePresets.All.Count, DraftGuidelinePresets.All.Select(p => p.Title).Distinct().Count());
        Assert.Contains(DraftGuidelinePresets.All, p => p.Title == "Zero RB" && p.Body.Contains("Avoid RB", StringComparison.Ordinal));
        Assert.Contains(DraftGuidelinePresets.All, p => p.Title == "Superflex QB" && p.Body.Contains("Superflex", StringComparison.Ordinal));
    }

    [Fact]
    public void Keeper_upside_preset_targets_late_round_upside_without_overriding_needs()
    {
        Assert.Contains("keeper league", DraftGuidelinePresets.KeeperUpside.Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("round 4 or later", DraftGuidelinePresets.KeeperUpside.Body, StringComparison.Ordinal);
        Assert.Contains("rookies", DraftGuidelinePresets.KeeperUpside.Body, StringComparison.Ordinal);
        Assert.Contains("tiebreaker, not an override", DraftGuidelinePresets.KeeperUpside.Body, StringComparison.Ordinal);
        Assert.Contains(DraftGuidelinePresets.All, p => p == DraftGuidelinePresets.KeeperUpside);
    }
}
