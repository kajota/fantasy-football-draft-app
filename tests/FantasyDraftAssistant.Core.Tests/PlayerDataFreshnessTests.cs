using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class PlayerDataFreshnessTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 18, 0, 0, TimeSpan.Zero);
    private static readonly TimeZoneInfo Eastern = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    [Fact]
    public void Never_synced_warns()
    {
        var warning = PlayerDataFreshness.Warning([], "sleeper", Now, Eastern);

        Assert.Equal(
            "Player data has not been synced. Refresh on Player Data so ranks, ADP, and injury status stay current.",
            warning);
    }

    [Fact]
    public void Fresh_sync_is_silent()
    {
        var warning = PlayerDataFreshness.Warning(
            [Info("sleeper", Now.AddHours(-23))],
            "sleeper",
            Now,
            Eastern);

        Assert.Null(warning);
    }

    [Fact]
    public void Exactly_24_hours_warns()
    {
        var stamp = Now.AddHours(-24);
        var warning = PlayerDataFreshness.Warning([Info("sleeper", stamp)], "sleeper", Now, Eastern);

        Assert.Equal(
            "Player data last synced 24h ago (Aug 23, 2026 2:00 PM). Refresh on Player Data so ranks, ADP, and injury status stay current.",
            warning);
    }

    [Fact]
    public void Stale_sync_warns_with_age()
    {
        var stamp = Now.AddDays(-3);
        var warning = PlayerDataFreshness.Warning([Info("sleeper", stamp)], "sleeper", Now, Eastern);

        Assert.Equal(
            "Player data last synced 3d ago (Aug 21, 2026 2:00 PM). Refresh on Player Data so ranks, ADP, and injury status stay current.",
            warning);
    }

    [Fact]
    public void Fantasypros_sheet_uses_fantasypros_provider()
    {
        var warning = PlayerDataFreshness.Warning(
            [Info("fantasypros", Now.AddHours(-2)), Info("sleeper", Now.AddDays(-4))],
            "fantasypros-half",
            Now,
            Eastern);

        Assert.Null(warning);
    }

    [Fact]
    public void Selected_source_ignores_other_providers()
    {
        var warning = PlayerDataFreshness.Warning(
            [Info("fantasypros", Now.AddHours(-1)), Info("sleeper", Now.AddDays(-4))],
            "sleeper",
            Now,
            Eastern);

        Assert.Contains("4d ago", warning);
        Assert.Contains("Refresh on Player Data", warning);
    }

    [Fact]
    public void Missing_selected_source_is_never_synced_even_if_others_exist()
    {
        var warning = PlayerDataFreshness.Warning(
            [Info("fantasypros", Now)],
            "sleeper",
            Now,
            Eastern);

        Assert.Equal(
            "Player data has not been synced. Refresh on Player Data so ranks, ADP, and injury status stay current.",
            warning);
    }

    [Fact]
    public void Combine_joins_compatibility_and_freshness()
    {
        var combined = PlayerDataFreshness.Combine(
            "Player data mismatch: this league is PPR 1-QB.",
            "Player data last synced 3d ago.");

        Assert.Equal(
            "Player data mismatch: this league is PPR 1-QB. Player data last synced 3d ago.",
            combined);
        Assert.Equal("only one", PlayerDataFreshness.Combine("only one", null));
        Assert.Equal("", PlayerDataFreshness.Combine(null, "   "));
    }

    private static FantasyDataRefreshInfo Info(string provider, DateTimeOffset stamp) => new()
    {
        ProviderKey = provider,
        Dataset = "rankings",
        RefreshedAt = stamp,
        RecordCount = 10
    };
}
