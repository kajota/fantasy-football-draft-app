using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class ScoringCatalogTests
{
    [Fact]
    public void Profile_name_distinguishes_ppr_and_superflex()
    {
        var half = ScoringCatalog.HalfPpr().Select(ToRule).ToList();
        var ppr = ScoringCatalog.Ppr().Select(ToRule).ToList();
        Assert.Equal("Half PPR 1-QB", ScoringCatalog.ProfileName(half, superflex: false));
        Assert.Equal("PPR Superflex", ScoringCatalog.ProfileName(ppr, superflex: true));
    }

    [Fact]
    public void Line_uses_human_label_and_unit()
    {
        Assert.Equal("Reception (PPR): 0.5 per catch", ScoringCatalog.Line(ScoringCategory.Reception, 0.5m));
        Assert.Equal("Receiving yards: 0.1 per yard", ScoringCatalog.Line(ScoringCategory.ReceivingYard, 0.1m));
        Assert.Equal("Field goal 40–49 yards: 4 each", ScoringCatalog.Line(ScoringCategory.FieldGoal40To49, 4m));
    }

    [Fact]
    public void Presets_include_yahoo_style_field_goal_bands()
    {
        var half = ScoringCatalog.HalfPpr();
        Assert.Equal(3m, half.Single(r => r.Category == ScoringCategory.FieldGoal0To19).Points);
        Assert.Equal(3m, half.Single(r => r.Category == ScoringCategory.FieldGoal20To29).Points);
        Assert.Equal(3m, half.Single(r => r.Category == ScoringCategory.FieldGoal30To39).Points);
        Assert.Equal(4m, half.Single(r => r.Category == ScoringCategory.FieldGoal40To49).Points);
        Assert.Equal(5m, half.Single(r => r.Category == ScoringCategory.FieldGoal50Plus).Points);
        Assert.Equal(2m, half.Single(r => r.Category == ScoringCategory.ExtraPointReturned).Points);
        Assert.DoesNotContain(half, r => r.Category == ScoringCategory.FieldGoal);
    }

    [Fact]
    public void Completes_legacy_flat_field_goal_into_short_bands()
    {
        var leagueId = LeagueId.New();
        var saved = new[]
        {
            new ScoringRule
            {
                ScoringRuleId = ScoringRuleId.New(),
                LeagueId = leagueId,
                Category = ScoringCategory.FieldGoal,
                Points = 3m
            }
        };

        var complete = ScoringCatalog.Complete(leagueId, saved);
        Assert.Equal(3m, complete.Single(r => r.Category == ScoringCategory.FieldGoal0To19).Points);
        Assert.Equal(3m, complete.Single(r => r.Category == ScoringCategory.FieldGoal30To39).Points);
        Assert.Equal(4m, complete.Single(r => r.Category == ScoringCategory.FieldGoal40To49).Points);
        Assert.Equal(5m, complete.Single(r => r.Category == ScoringCategory.FieldGoal50Plus).Points);
        Assert.DoesNotContain(complete, r => r.Category == ScoringCategory.FieldGoal);
    }

    [Fact]
    public void Reception_help_separates_ppr_from_yards()
    {
        var reception = ScoringCatalog.All.Single(i => i.Category == ScoringCategory.Reception);
        var yards = ScoringCatalog.All.Single(i => i.Category == ScoringCategory.ReceivingYard);
        Assert.Contains("PPR", reception.Help, StringComparison.Ordinal);
        Assert.Contains("10 yards per point", reception.Help, StringComparison.Ordinal);
        Assert.Contains("10 yards per point", yards.Help, StringComparison.Ordinal);
        Assert.Contains("not PPR", yards.Help, StringComparison.OrdinalIgnoreCase);
        Assert.All(ScoringCatalog.All, info => Assert.False(string.IsNullOrWhiteSpace(info.Help)));
        Assert.All(ScoringCatalog.All, info => Assert.False(string.IsNullOrWhiteSpace(info.Unit)));

        var returned = ScoringCatalog.All.Single(i => i.Category == ScoringCategory.ExtraPointReturned);
        Assert.Equal(2m, returned.DefaultPoints);
        Assert.Contains("returned", returned.Help, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("2", returned.Help, StringComparison.Ordinal);
    }

    private static ScoringRule ToRule(ScoringPreset preset) => new()
    {
        ScoringRuleId = ScoringRuleId.New(),
        LeagueId = LeagueId.New(),
        Category = preset.Category,
        Points = preset.Points
    };
}
