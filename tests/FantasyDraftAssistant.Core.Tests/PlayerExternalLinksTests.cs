using FantasyDraftAssistant.Core.Analytics;

namespace FantasyDraftAssistant.Core.Tests;

public class PlayerExternalLinksTests
{
    [Fact]
    public void FantasyPros_uses_a_name_slug()
    {
        Assert.Equal(
            "https://www.fantasypros.com/nfl/players/bijan-robinson.php",
            PlayerExternalLinks.FantasyPros("Bijan Robinson", "RB"));
        Assert.Equal(
            "https://www.fantasypros.com/nfl/players/jamarr-chase.php",
            PlayerExternalLinks.FantasyPros("Ja'Marr Chase", "WR"));
    }

    [Fact]
    public void FantasyPros_adds_position_for_josh_allen()
    {
        Assert.Equal(
            "https://www.fantasypros.com/nfl/players/josh-allen-qb.php",
            PlayerExternalLinks.FantasyPros("Josh Allen", "QB"));
    }

    [Fact]
    public void Sleeper_needs_their_player_id()
    {
        Assert.Null(PlayerExternalLinks.Sleeper("Josh Allen", null));
        Assert.Equal(
            "https://sleeper.com/nfl/players/josh-allen-4984",
            PlayerExternalLinks.Sleeper("Josh Allen", "4984"));
    }

    [Fact]
    public void Yahoo_uses_the_numeric_id()
    {
        Assert.Null(PlayerExternalLinks.Yahoo(null));
        Assert.Equal(
            "https://sports.yahoo.com/nfl/players/31002",
            PlayerExternalLinks.Yahoo("31002"));
    }
}
