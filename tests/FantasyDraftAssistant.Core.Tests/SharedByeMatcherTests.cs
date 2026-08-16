using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Tests;

public class SharedByeMatcherTests
{
    [Fact]
    public void Same_position_and_bye_matches()
    {
        var gibbs = Rb("Jahmyr Gibbs", 8);
        var brown = Rb("Chase Brown", 8);
        var match = SharedByeMatcher.For(brown, [gibbs]);
        Assert.NotNull(match);
        Assert.Equal(8, match.ByeWeek);
        Assert.Equal("Jahmyr Gibbs", SharedByeMatcher.Teammates(match));
        Assert.Equal("Bye 8 · Jahmyr Gibbs", SharedByeMatcher.Label(match));
        Assert.Contains("Jahmyr Gibbs", SharedByeMatcher.Detail(match), StringComparison.Ordinal);
    }

    [Fact]
    public void Different_position_or_bye_does_not_match()
    {
        var gibbs = Rb("Jahmyr Gibbs", 8);
        Assert.Null(SharedByeMatcher.For(new RosterByePlayer("Amon-Ra St. Brown", PlayerPosition.WR, 8), [gibbs]));
        Assert.Null(SharedByeMatcher.For(Rb("Saquon Barkley", 9), [gibbs]));
    }

    [Fact]
    public void Missing_bye_does_not_match()
    {
        var gibbs = Rb("Jahmyr Gibbs", 8);
        Assert.Null(SharedByeMatcher.For(Rb("Mystery Back", null), [gibbs]));
        Assert.Null(SharedByeMatcher.For(Rb("Chase Brown", 8), [Rb("Unknown", null)]));
    }

    [Fact]
    public void Lists_every_same_position_teammate_on_that_bye()
    {
        var match = SharedByeMatcher.For(
            Rb("Breece Hall", 7),
            [Rb("Jahmyr Gibbs", 7), Rb("James Cook", 7), Wr("Tyreek Hill", 7)]);
        Assert.NotNull(match);
        Assert.Equal("Jahmyr Gibbs, James Cook", SharedByeMatcher.Teammates(match));
        Assert.Equal("Bye 7 · Jahmyr Gibbs, James Cook", SharedByeMatcher.Label(match));
    }

    private static RosterByePlayer Rb(string name, int? bye) =>
        new(name, PlayerPosition.RB, bye);

    private static RosterByePlayer Wr(string name, int? bye) =>
        new(name, PlayerPosition.WR, bye);
}
