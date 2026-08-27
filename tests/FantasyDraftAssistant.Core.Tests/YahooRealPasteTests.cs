using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Core.Tests;

/// Drives the parser with a genuine Ctrl+A/Ctrl+C capture of a real private Yahoo
/// league — the Scoring &amp; Settings page and the Managers page. Email addresses are
/// the only thing altered. Every assertion here corresponds to something the
/// synthetic fixtures got wrong, so these are the tests that actually guard the
/// shapes Yahoo emits.
public class YahooRealPasteTests
{
    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private static YahooLeagueSnapshot Snapshot()
    {
        var result = YahooPasteParser.Parse(new YahooPasteInput
        {
            LeagueUrlOrId = null,
            SettingsText = Fixture("yahoo-settings-paste.txt"),
            TeamsText = Fixture("yahoo-managers-paste.txt")
        });

        Assert.True(result.Succeeded, string.Join(" | ", result.MissingSections));
        return result.Snapshot!;
    }

    [Fact]
    public void Reads_the_league_header_without_a_url()
    {
        var snapshot = Snapshot();

        // "League ID#: <tab> 658531" makes the League URL box optional.
        Assert.Equal("nfl.l.658531", snapshot.LeagueKey);
        Assert.Equal("Strata", snapshot.Name);
        Assert.Equal(10, snapshot.TeamCount);
        Assert.False(snapshot.IsAuction);

        // The page has no Season row; 2026 appears only in the trade deadline
        // and the footer copyright, both far below the top of the document.
        Assert.Equal(2026, snapshot.Season);
    }

    [Fact]
    public void Strips_the_logo_alt_text_from_every_team_name()
    {
        var names = Snapshot().Teams.Select(t => t.Name).ToList();

        Assert.DoesNotContain(names, n => n.StartsWith("logo", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Al's Agreeable Team", names);
        Assert.Contains("Wet Whispers", names);
    }

    [Fact]
    public void Rejoins_rows_that_wrap_instead_of_inventing_extra_teams()
    {
        var teams = Snapshot().Teams;

        // Exactly the 10 real teams — no phantom row built from a wrapped line.
        Assert.Equal(10, teams.Count);

        // "logo Ginger Cool <tab> Kelly \n Norton <tab> ..." — the manager name wraps.
        var mine = teams.Single(t => t.Name == "Ginger Cool");
        Assert.Equal("Kelly Norton", mine.OwnerName);

        // "logo Golden \n Gate Graves <tab> John <tab> ..." — the team name wraps.
        Assert.Equal("John", teams.Single(t => t.Name == "Golden Gate Graves").OwnerName);
    }

    [Fact]
    public void Decodes_punctuation_yahoo_leaves_as_a_literal_escape()
    {
        var names = Snapshot().Teams.Select(t => t.Name).ToList();

        // The copied page carries the six characters \u2019 where an apostrophe belongs.
        Assert.DoesNotContain(names, n => n.Contains("\\u", StringComparison.Ordinal));
        Assert.Contains("Nat\u2019s Nifty Team", names);
        Assert.Contains("Rob\u2019s", names);
    }

    [Fact]
    public void Never_takes_an_email_address_as_the_owner()
    {
        var teams = Snapshot().Teams;

        Assert.DoesNotContain(teams, t => t.OwnerName?.Contains('@') == true);
        Assert.DoesNotContain(teams, t => t.Name.Contains('@'));
    }

    [Fact]
    public void Takes_the_league_value_column_not_yahoos_default()
    {
        var scoring = YahooLeagueMapper.Map(Snapshot()).Request.Scoring
            .ToDictionary(rule => rule.Category, rule => rule.Points);

        // Both of these wrap across three lines with the league value first and
        // Yahoo's default second. Reading the wrong column silently downgrades a
        // full-PPR, 6-point-passing-TD league to Yahoo's stock scoring.
        Assert.Equal(6m, scoring[ScoringCategory.PassingTouchdown]);   // Yahoo default is 4
        Assert.Equal(1m, scoring[ScoringCategory.Reception]);          // Yahoo default is 0.5
    }

    [Fact]
    public void Reads_the_single_column_scoring_rows_too()
    {
        var scoring = YahooLeagueMapper.Map(Snapshot()).Request.Scoring
            .ToDictionary(rule => rule.Category, rule => rule.Points);

        Assert.Equal(0.04m, scoring[ScoringCategory.PassingYard]);     // "25 yards per point"
        Assert.Equal(0.1m, scoring[ScoringCategory.RushingYard]);      // "10 yards per point"
        Assert.Equal(0.1m, scoring[ScoringCategory.ReceivingYard]);
        Assert.Equal(-1m, scoring[ScoringCategory.Interception]);      // thrown
        Assert.Equal(2m, scoring[ScoringCategory.DefensiveInterception]);
        Assert.Equal(6m, scoring[ScoringCategory.DefensiveTouchdown]);
        Assert.Equal(-2m, scoring[ScoringCategory.FumbleLost]);
        Assert.Equal(5m, scoring[ScoringCategory.FieldGoal50Plus]);
        Assert.Equal(-4m, scoring[ScoringCategory.PointsAllowed35Plus]);
        Assert.Equal(2m, scoring[ScoringCategory.ExtraPointReturned]);
    }

    [Fact]
    public void Does_not_treat_ordinary_settings_rows_as_scoring()
    {
        var labels = Snapshot().Stats.Select(s => s.DisplayName).ToList();

        Assert.DoesNotContain("fractional points", labels);
        Assert.DoesNotContain("votes required to veto", labels);
        Assert.DoesNotContain("max teams", labels);
    }

    [Fact]
    public void Reads_the_superflex_roster()
    {
        var roster = YahooLeagueMapper.Map(Snapshot()).Request.Roster
            .ToDictionary(slot => slot.SlotCode, slot => slot.Count);

        Assert.Equal(1, roster["QB"]);
        Assert.Equal(2, roster["WR"]);
        Assert.Equal(2, roster["RB"]);
        Assert.Equal(1, roster["TE"]);
        Assert.Equal(1, roster["Q/W/R/T"]);
        Assert.Equal(1, roster["K"]);
        Assert.Equal(1, roster["DEF"]);
        Assert.Equal(5, roster["BN"]);
        Assert.Equal(2, roster["IR"]);
    }

    [Fact]
    public void Says_that_it_could_not_tell_which_team_is_yours()
    {
        var result = YahooPasteParser.Parse(new YahooPasteInput
        {
            SettingsText = Fixture("yahoo-settings-paste.txt"),
            TeamsText = Fixture("yahoo-managers-paste.txt")
        });

        Assert.Contains(result.Warnings, w => w.Contains("which team is yours"));
    }
}
