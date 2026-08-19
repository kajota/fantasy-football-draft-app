using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.Core.Tests;

public class DecisionContextMathTests
{
    private static PlayerSummaryDto Player(
        string name,
        string position,
        decimal? projected = null,
        int? tier = null,
        double? adp = null,
        double? rankStd = null)
    {
        return new PlayerSummaryDto
        {
            PlayerId = name,
            Name = name,
            Position = position,
            NflTeam = "ATL",
            Status = "Active",
            ProjectedPoints = projected,
            Tier = tier,
            OverallAdp = adp,
            RankStd = rankStd
        };
    }

    [Fact]
    public void Outlook_is_null_without_adp_or_next_pick()
    {
        Assert.Null(PickOutlook.For(null, null, 20));
        Assert.Null(PickOutlook.For(12.0, null, null));
    }

    [Fact]
    public void Adp_well_before_next_pick_is_likely_gone()
    {
        Assert.Equal(PickOutlook.LikelyGone, PickOutlook.For(5.0, null, 20));
    }

    [Fact]
    public void Adp_well_after_next_pick_is_likely_back()
    {
        Assert.Equal(PickOutlook.LikelyBack, PickOutlook.For(40.0, null, 20));
    }

    [Fact]
    public void Adp_near_next_pick_is_a_coin_flip()
    {
        Assert.Equal(PickOutlook.CoinFlip, PickOutlook.For(18.0, null, 20));
        Assert.Equal(PickOutlook.CoinFlip, PickOutlook.For(23.0, null, 20));
    }

    [Fact]
    public void High_expert_disagreement_widens_the_coin_flip_band()
    {
        // ADP 10 with next pick 20: gone with the default 6-pick margin,
        // but a volatile rank spread stretches the band to cover it.
        Assert.Equal(PickOutlook.LikelyGone, PickOutlook.For(10.0, null, 20));
        Assert.Equal(PickOutlook.CoinFlip, PickOutlook.For(10.0, 8.0, 20));
    }

    [Fact]
    public void Margin_is_clamped()
    {
        Assert.Equal(4.0, PickOutlook.Margin(0.5));
        Assert.Equal(12.0, PickOutlook.Margin(40.0));
        Assert.Equal(6.0, PickOutlook.Margin(null));
    }

    [Fact]
    public void Replacement_baseline_uses_remaining_league_demand()
    {
        var available = new[]
        {
            Player("RB One", "RB", projected: 300m),
            Player("RB Two", "RB", projected: 250m),
            Player("RB Three", "RB", projected: 200m),
            Player("RB Four", "RB", projected: 150m)
        };
        var baselines = ValueOverReplacement.Baselines(
            available,
            new Dictionary<string, int> { ["RB"] = 3 });
        Assert.Equal(200m, baselines["RB"]);
    }

    [Fact]
    public void Replacement_baseline_clamps_to_pool_size()
    {
        var available = new[] { Player("RB One", "RB", projected: 300m) };
        var baselines = ValueOverReplacement.Baselines(
            available,
            new Dictionary<string, int> { ["RB"] = 10 });
        Assert.Equal(300m, baselines["RB"]);
    }

    [Fact]
    public void Positions_without_demand_or_projections_have_no_baseline()
    {
        var available = new[]
        {
            Player("RB One", "RB", projected: 300m),
            Player("WR One", "WR")
        };
        var baselines = ValueOverReplacement.Baselines(available, new Dictionary<string, int> { ["WR"] = 2 });
        Assert.Empty(baselines);
    }

    [Fact]
    public void Tier_cliffs_count_the_best_remaining_tier_per_position()
    {
        var available = new[]
        {
            Player("RB One", "RB", tier: 2),
            Player("RB Two", "RB", tier: 2),
            Player("RB Three", "RB", tier: 4),
            Player("QB One", "QB", tier: 1),
            Player("No Tier", "WR")
        };
        var lines = TierCliffSummary.Build(available);
        Assert.Contains("RB: 2 left in Tier 2, next tier is 4", lines);
        Assert.Contains("QB: 1 left in Tier 1, no later tier cached", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("WR", StringComparison.Ordinal));
    }

    [Fact]
    public void Freshness_age_reads_naturally()
    {
        var now = new DateTimeOffset(2026, 8, 17, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal("just now", FreshnessAge.Describe(now.AddSeconds(-20), now));
        Assert.Equal("25m ago", FreshnessAge.Describe(now.AddMinutes(-25), now));
        Assert.Equal("14h ago", FreshnessAge.Describe(now.AddHours(-14), now));
        Assert.Equal("3d ago", FreshnessAge.Describe(now.AddDays(-3), now));
    }
}
