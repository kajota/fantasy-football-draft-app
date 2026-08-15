using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Core.Tests;

public class YahooLeagueMapperTests
{
    [Fact]
    public void Maps_superflex_roster_scoring_and_user_team()
    {
        var mapped = YahooLeagueMapper.Map(SampleSnapshot());

        Assert.Equal("Sunday Night", mapped.Request.Name);
        Assert.Equal(2026, mapped.Request.Season);
        Assert.Equal(FantasyPlatform.Yahoo, mapped.Request.Platform);
        Assert.Equal("449.l.555", mapped.Request.ExternalLeagueId);
        Assert.Equal(DraftType.Snake, mapped.Request.DraftType);
        Assert.Equal(16, mapped.Request.RoundCount);
        Assert.Equal(12, mapped.Request.Teams.Count);
        Assert.Equal("My Squad", mapped.Request.Teams.Single(t => t.IsUserTeam).Name);
        Assert.Contains(mapped.Request.Roster, s => s.SlotCode == "Q/W/R/T" && s.Count == 1);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.Reception && s.Points == 0.5m);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.PassingTouchdown && s.Points == 4m);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.FieldGoal0To19 && s.Points == 3m);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.FieldGoal40To49 && s.Points == 4m);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.FieldGoal50Plus && s.Points == 5m);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.Sack && s.Points == 1m);
        Assert.Contains(mapped.Request.Scoring, s => s.Category == ScoringCategory.ExtraPointReturned && s.Points == 2m);
        Assert.DoesNotContain(mapped.Request.Scoring, s => s.Category == ScoringCategory.TwoPointConversion && s.Points == 3m);
        Assert.Contains(mapped.ReviewItems, item => item.Contains("Draft Order", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(mapped.ReviewItems, item => item.Contains("keeper", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Rejects_auction_leagues()
    {
        var snapshot = SampleSnapshot() with { IsAuction = true };
        var error = Assert.Throws<InvalidOperationException>(() => YahooLeagueMapper.Map(snapshot));
        Assert.Contains("Auction", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Records_unmapped_idp_slots_without_dropping_known_ones()
    {
        var snapshot = SampleSnapshot() with
        {
            Roster =
            [
                new YahooRosterPosition { Position = "QB", Count = 1 },
                new YahooRosterPosition { Position = "BN", Count = 6 },
                new YahooRosterPosition { Position = "LB", Count = 2 }
            ]
        };

        var mapped = YahooLeagueMapper.Map(snapshot);
        Assert.Contains(mapped.Request.Roster, s => s.SlotCode == "QB");
        Assert.Contains(mapped.UnmappedRosterSlots, s => s.Contains("LB"));
    }

    [Fact]
    public void Uses_defaults_when_yahoo_omits_roster_and_scoring()
    {
        var snapshot = SampleSnapshot() with { Roster = [], Stats = [], DraftRounds = null };
        var mapped = YahooLeagueMapper.Map(snapshot);
        Assert.NotEmpty(mapped.Request.Roster);
        Assert.NotEmpty(mapped.Request.Scoring);
        Assert.True(mapped.Request.RoundCount >= 15);
        Assert.Contains(mapped.ReviewItems, item => item.Contains("scoring", StringComparison.OrdinalIgnoreCase));
    }

    private static YahooLeagueSnapshot SampleSnapshot()
    {
        var teams = Enumerable.Range(1, 12).Select(i => new YahooTeamSnapshot
        {
            TeamKey = $"449.l.555.t.{i}",
            Name = i == 3 ? "My Squad" : $"Team {i}",
            OwnerName = i == 3 ? "Kelly" : $"Owner {i}",
            TeamNumber = i,
            IsCurrentUser = i == 3
        }).ToList();

        return new YahooLeagueSnapshot
        {
            LeagueKey = "449.l.555",
            Name = "Sunday Night",
            Season = 2026,
            TeamCount = 12,
            IsAuction = false,
            IsKeeper = true,
            DraftTypeRaw = "live",
            DraftRounds = 16,
            Roster =
            [
                new YahooRosterPosition { Position = "QB", Count = 1 },
                new YahooRosterPosition { Position = "WR", Count = 2 },
                new YahooRosterPosition { Position = "RB", Count = 2 },
                new YahooRosterPosition { Position = "TE", Count = 1 },
                new YahooRosterPosition { Position = "W/R/T", Count = 1 },
                new YahooRosterPosition { Position = "Q/W/R/T", Count = 1 },
                new YahooRosterPosition { Position = "K", Count = 1 },
                new YahooRosterPosition { Position = "DEF", Count = 1 },
                new YahooRosterPosition { Position = "BN", Count = 6 }
            ],
            Stats =
            [
                new YahooStatModifier { StatId = 4, Value = 0.04m },
                new YahooStatModifier { StatId = 5, Value = 4m },
                new YahooStatModifier { StatId = 11, Value = 0.5m },
                new YahooStatModifier { StatId = 16, Value = 2m },
                new YahooStatModifier { StatId = 19, Value = 3m },
                new YahooStatModifier { StatId = 20, Value = 3m },
                new YahooStatModifier { StatId = 21, Value = 3m },
                new YahooStatModifier { StatId = 22, Value = 4m },
                new YahooStatModifier { StatId = 23, Value = 5m },
                new YahooStatModifier { StatId = 29, Value = 1m },
                new YahooStatModifier { StatId = 82, Value = 2m },
                new YahooStatModifier { StatId = 32, Value = 1m },
                new YahooStatModifier { StatId = 88, Value = 3m, DisplayName = "Return TD" }
            ],
            Teams = teams
        };
    }
}
