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
        Assert.Equal(8, DraftGuidelinePresets.All.Count);
        Assert.Equal(DraftGuidelinePresets.All.Count, DraftGuidelinePresets.All.Select(p => p.Title).Distinct().Count());
        Assert.Contains(DraftGuidelinePresets.All, p => p.Title == "Zero RB" && p.Body.Contains("Avoid RB", StringComparison.Ordinal));
        Assert.Contains(DraftGuidelinePresets.All, p => p.Title == "Superflex QB" && p.Body.Contains("Superflex", StringComparison.Ordinal));
    }
}
