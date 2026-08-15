using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Providers.FantasyData;

namespace FantasyDraftAssistant.Data.Tests;

public class SleeperCatalogTests
{
    [Fact]
    public void Maps_skill_player_name_team_and_rank_filter()
    {
        var player = SleeperCatalog.ToPlayer(new SleeperPlayerDto
        {
            PlayerId = "4984",
            FullName = "Josh Allen",
            Team = "BUF",
            Position = "QB",
            SearchRank = 3,
            InjuryStatus = "Questionable"
        }, DateTimeOffset.UtcNow);

        Assert.NotNull(player);
        Assert.Equal("Josh Allen", player.Name);
        Assert.Equal("BUF", player.NflTeam);
        Assert.Equal(PlayerPosition.QB, player.PrimaryPosition);
        Assert.Equal(PlayerStatus.Questionable, player.Status);
    }

    [Fact]
    public void Maps_zero_years_exp_as_a_rookie()
    {
        var player = SleeperCatalog.ToPlayer(new SleeperPlayerDto
        {
            PlayerId = "9001",
            FullName = "First Year",
            Team = "SEA",
            Position = "WR",
            SearchRank = 20,
            YearsExp = 0
        }, DateTimeOffset.UtcNow);

        Assert.NotNull(player);
        Assert.Equal(0, player.YearsExp);
        Assert.True(player.IsRookie);
    }

    [Fact]
    public void Maps_defense_without_full_name()
    {
        var player = SleeperCatalog.ToPlayer(new SleeperPlayerDto
        {
            PlayerId = "ARI",
            FirstName = "Arizona",
            LastName = "Cardinals",
            Team = "ARI",
            Position = "DEF"
        }, DateTimeOffset.UtcNow);

        Assert.NotNull(player);
        Assert.Equal("Arizona Cardinals", player.Name);
        Assert.Equal(PlayerPosition.DEF, player.PrimaryPosition);
    }

    [Fact]
    public void Drops_unranked_backups()
    {
        var player = SleeperCatalog.ToPlayer(new SleeperPlayerDto
        {
            PlayerId = "1",
            FullName = "Camp Arm",
            Team = "NYG",
            Position = "QB",
            SearchRank = 9000
        }, DateTimeOffset.UtcNow);

        Assert.Null(player);
    }

    [Fact]
    public void Prefers_2qb_adp_when_present()
    {
        var adp = SleeperCatalog.ChooseAdp(new SleeperProjectionDto
        {
            Adp2Qb = 4.2,
            AdpPpr = 28.1
        });
        Assert.Equal(4.2, adp);
    }

    [Fact]
    public void Maps_injured_reserve_from_roster_status()
    {
        var player = SleeperCatalog.ToPlayer(new SleeperPlayerDto
        {
            PlayerId = "2",
            FullName = "Hurt Back",
            Team = "CHI",
            Position = "RB",
            SearchRank = 40,
            Status = "Injured Reserve"
        }, DateTimeOffset.UtcNow);

        Assert.NotNull(player);
        Assert.Equal(PlayerStatus.InjuredReserve, player.Status);
    }

    [Fact]
    public void Maps_injury_body_part_and_notes()
    {
        var player = SleeperCatalog.ToPlayer(new SleeperPlayerDto
        {
            PlayerId = "3",
            FullName = "Banged Up",
            Team = "KC",
            Position = "WR",
            SearchRank = 15,
            InjuryStatus = "Questionable",
            InjuryBodyPart = "Hamstring",
            InjuryNotes = "Limited Wednesday",
            InjuryStartDate = "2026-08-10"
        }, DateTimeOffset.UtcNow);

        Assert.NotNull(player);
        Assert.Equal(PlayerStatus.Questionable, player.Status);
        Assert.Equal("Hamstring", player.InjuryBodyPart);
        Assert.Equal("Limited Wednesday", player.InjuryNotes);
        Assert.Equal("2026-08-10", player.InjuryStartedOn);
        Assert.Contains("Hamstring", player.InjuryLine, StringComparison.Ordinal);
        Assert.Equal("Q", player.StatusCode);
    }
}
