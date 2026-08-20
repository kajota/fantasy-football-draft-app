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
        // Straddling the pick from either side leaves the call genuinely open.
        Assert.Equal(PickOutlook.CoinFlip, PickOutlook.For(18.0, null, 20));
        Assert.Equal(PickOutlook.CoinFlip, PickOutlook.For(21.0, null, 20));
    }

    [Fact]
    public void High_expert_disagreement_pulls_the_call_toward_a_coin_flip()
    {
        // ADP 14 with next pick 20 is a comfortable "gone" when sources agree; once they
        // disagree badly the same gap is no longer enough to call it.
        Assert.Equal(PickOutlook.LikelyGone, PickOutlook.For(14.0, null, 20));
        Assert.Equal(PickOutlook.CoinFlip, PickOutlook.For(14.0, 10.0, 20));
    }

    [Fact]
    public void Sigma_is_clamped()
    {
        Assert.Equal(2.0, PickOutlook.Sigma(0.5));
        Assert.Equal(10.0, PickOutlook.Sigma(40.0));
        Assert.Equal(4.0, PickOutlook.Sigma(null));
    }

    [Fact]
    public void Gone_probability_is_a_normal_cdf_around_adp()
    {
        // ADP exactly on the user's next pick is the definition of a toss-up.
        Assert.Equal(0.5, PickOutlook.GoneProbability(20.0, null, 20)!.Value, 3);

        // One sigma earlier than the pick is the standard ~84%.
        Assert.Equal(0.841, PickOutlook.GoneProbability(16.0, 4.0, 20)!.Value, 3);
        // One sigma later mirrors it.
        Assert.Equal(0.159, PickOutlook.GoneProbability(24.0, 4.0, 20)!.Value, 3);

        // Far either side saturates.
        Assert.True(PickOutlook.GoneProbability(1.0, null, 40) > 0.999);
        Assert.True(PickOutlook.GoneProbability(80.0, null, 20) < 0.001);
    }

    [Fact]
    public void Wider_disagreement_pulls_the_probability_toward_a_toss_up()
    {
        var tight = PickOutlook.GoneProbability(10.0, 2.0, 20)!.Value;
        var loose = PickOutlook.GoneProbability(10.0, 10.0, 20)!.Value;

        Assert.True(tight > loose);
        Assert.True(tight > 0.99);
        Assert.InRange(loose, 0.75, 0.90);
    }

    [Fact]
    public void Gone_probability_needs_an_adp_and_a_next_pick()
    {
        Assert.Null(PickOutlook.GoneProbability(null, 4.0, 20));
        Assert.Null(PickOutlook.GoneProbability(20.0, 4.0, null));
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
        // Tier 3 is already gone, so naming the next tier tells you the drop is steeper.
        Assert.Contains("RB: 2 left in Tier 2, then Tier 4", lines);
        Assert.Contains("QB: 1 left in Tier 1", lines);
        Assert.DoesNotContain(lines, line => line.StartsWith("WR", StringComparison.Ordinal));
    }

    [Fact]
    public void Tier_cliffs_stay_quiet_when_the_next_tier_is_just_the_next_number()
    {
        var available = new[]
        {
            Player("RB One", "RB", tier: 2),
            Player("RB Two", "RB", tier: 3)
        };

        // "next tier is 3" after Tier 2 carries no information, so it is left out.
        var line = Assert.Single(TierCliffSummary.Build(available));
        Assert.Equal("RB: 1 left in Tier 2", line);
    }

    [Fact]
    public void Tier_cliff_detail_carries_the_same_numbers_as_the_prose()
    {
        var available = new[]
        {
            Player("RB One", "RB", tier: 2),
            Player("RB Two", "RB", tier: 2),
            Player("RB Three", "RB", tier: 4),
            Player("QB One", "QB", tier: 1)
        };

        var detail = TierCliffSummary.Detail(available);
        var rb = Assert.Single(detail, cliff => cliff.Position == "RB");
        Assert.Equal(2, rb.Remaining);
        Assert.Equal(2, rb.Tier);
        Assert.Equal(4, rb.NextTier);

        var qb = Assert.Single(detail, cliff => cliff.Position == "QB");
        Assert.Null(qb.NextTier);

        // The prose form the AI sees must stay derived from the same records.
        Assert.Equal(TierCliffSummary.Build(available), detail.Select(cliff => cliff.Line));
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
