using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Tests;

public class HandcuffMatcherTests
{
    [Fact]
    public void Same_team_worse_rb_is_a_handcuff()
    {
        var bijan = Rb("Bijan Robinson", "ATL", rank: 1);
        var allgeier = Rb("Tyler Allgeier", "ATL", rank: 80);
        var match = HandcuffMatcher.For(allgeier, [bijan]);
        Assert.NotNull(match);
        Assert.Equal("Bijan Robinson", match.StarterName);
        Assert.Equal("Cuff · Bijan Robinson", HandcuffMatcher.Label(match));
    }

    [Fact]
    public void Better_rb_on_the_same_team_is_not_a_handcuff()
    {
        var bijan = Rb("Bijan Robinson", "ATL", rank: 1);
        var allgeier = Rb("Tyler Allgeier", "ATL", rank: 80);
        Assert.Null(HandcuffMatcher.For(bijan, [allgeier]));
    }

    [Fact]
    public void Other_positions_and_teams_are_ignored()
    {
        var bijan = Rb("Bijan Robinson", "ATL", rank: 1);
        Assert.Null(HandcuffMatcher.For(new HandcuffPlayer("Drake London", "ATL", PlayerPosition.WR, 12, null), [bijan]));
        Assert.Null(HandcuffMatcher.For(Rb("Braelon Allen", "NYJ", 90), [bijan]));
    }

    [Fact]
    public void Same_team_backup_qb_matches()
    {
        var allen = new HandcuffPlayer("Josh Allen", "BUF", PlayerPosition.QB, 20, null);
        var backup = new HandcuffPlayer("Mitchell Trubisky", "BUF", PlayerPosition.QB, 180, null);
        var match = HandcuffMatcher.For(backup, [allen]);
        Assert.Equal("Josh Allen", match?.StarterName);
    }

    private static HandcuffPlayer Rb(string name, string team, int rank) =>
        new(name, team, PlayerPosition.RB, rank, null);
}
