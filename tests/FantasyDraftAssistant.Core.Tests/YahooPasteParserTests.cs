using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Core.Tests;

/// The fixtures below imitate a browser Ctrl+A/Ctrl+C of Yahoo's League Settings
/// and Teams pages: table cells arrive tab-separated, one row per line. Replace
/// them with a real capture from your own league to pin the parser to what Yahoo
/// actually emits today.
public class YahooPasteParserTests
{
    private const string SettingsPaste =
        "Fantasy Football\t2026 Season\n" +
        "Setting \tValue\n" +
        "League ID#: \t555123\n" +
        "League Name: \tSunday Night\n" +
        "League Logo: \t\n" +
        "Draft Type: \tLive Standard Draft\n" +
        "Max Teams: \t12\n" +
        "Scoring Type: \tHead-to-Head\n" +
        "Roster Positions: \tQB, WR, WR, RB, RB, TE, W/R/T, K, DEF, BN, BN, BN, BN, BN, BN, IR\n" +
        "Fractional Points: \tYes\n" +
        "Negative Points: \tYes\n" +
        "Offense \tLeague Value \tYahoo Default Value\n" +
        "Passing Yards \t25 yards per point \t\n" +
        "Passing Touchdowns \t4 \t\n" +
        "Interceptions \t-1 \t\n" +
        "Rushing Yards \t10 yards per point \t\n" +
        "Rushing Touchdowns \t6 \t\n" +
        "Reception \t0.5 \t\n" +
        "Receiving Yards \t10 yards per point \t\n" +
        "Receiving Touchdowns \t6 \t\n" +
        "2-Point Conversions \t2 \t\n" +
        "Fumbles Lost \t-2 \t\n" +
        "Kickers \tLeague Value \tYahoo Default Value\n" +
        "Field Goals 0-19 Yards \t3 \t\n" +
        "Field Goals 20-29 Yards \t3 \t\n" +
        "Field Goals 30-39 Yards \t3 \t\n" +
        "Field Goals 40-49 Yards \t4 \t\n" +
        "Field Goals 50+ Yards \t5 \t\n" +
        "Point After Attempt Made \t1 \t\n" +
        "Defense/Special Teams \tLeague Value \tYahoo Default Value\n" +
        "Sack \t1 \t\n" +
        "Interception \t2 \t\n" +
        "Fumble Recovery \t2 \t\n" +
        "Touchdown \t6 \t\n" +
        "Safety \t2 \t\n" +
        "Points Allowed 0 points \t10 \t\n" +
        "Points Allowed 1-6 points \t7 \t\n";

    private const string TeamsPaste =
        "Team Name \tManager \tEmail \tWaiver Priority \tMoves \tTrades \tLast League Activity\n" +
        "logo Thunder Ducks\tkelly\tone@example.com\t-\t0 \t0 \tWed Jul 22 12:42pm EDT\n" +
        "logo Gridiron Goons\tdave\ttwo@example.com\t-\t0 \t0 \tWed Jul 22 12:42pm EDT\n" +
        "logo Couch Commandos\tsam\tthree@example.com\t-\t0 \t0 \tWed Jul 22 12:42pm EDT\n" +
        "logo Red Zone Rejects\tpat\tfour@example.com\t-\t0 \t0 \tWed Jul 22 12:42pm EDT\n";

    private static YahooPasteInput Input(string? settings = null, string? teams = null) => new()
    {
        LeagueUrlOrId = "https://football.fantasysports.yahoo.com/f1/555123/settings",
        SettingsText = settings ?? SettingsPaste,
        TeamsText = teams ?? TeamsPaste
    };

    [Fact]
    public void Reads_league_header_from_a_settings_paste()
    {
        var result = YahooPasteParser.Parse(Input());

        Assert.True(result.Succeeded);
        Assert.Empty(result.MissingSections);
        var snapshot = result.Snapshot!;
        Assert.Equal("nfl.l.555123", snapshot.LeagueKey);
        Assert.Equal("Sunday Night", snapshot.Name);
        Assert.Equal(2026, snapshot.Season);
        Assert.False(snapshot.IsAuction);
        Assert.Equal("Live Standard Draft", snapshot.DraftTypeRaw);
    }

    [Fact]
    public void Tallies_repeated_roster_positions()
    {
        var roster = YahooPasteParser.Parse(Input()).Snapshot!.Roster
            .ToDictionary(p => p.Position, p => p.Count, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(1, roster["QB"]);
        Assert.Equal(2, roster["WR"]);
        Assert.Equal(2, roster["RB"]);
        Assert.Equal(1, roster["TE"]);
        Assert.Equal(1, roster["W/R/T"]);
        Assert.Equal(6, roster["BN"]);
        Assert.Equal(1, roster["IR"]);
    }

    [Theory]
    [InlineData("BN x 6", 6)]
    [InlineData("BN (6)", 6)]
    [InlineData("BN × 6", 6)]
    public void Accepts_roster_slots_written_with_a_multiplier(string token, int expected)
    {
        var settings = SettingsPaste.Replace(
            "QB, WR, WR, RB, RB, TE, W/R/T, K, DEF, BN, BN, BN, BN, BN, BN, IR",
            $"QB, WR, WR, RB, RB, TE, W/R/T, K, DEF, {token}, IR");

        var roster = YahooPasteParser.Parse(Input(settings)).Snapshot!.Roster;
        Assert.Equal(expected, roster.Single(p => p.Position == "BN").Count);
    }

    [Fact]
    public void Reads_teams_and_managers_in_page_order()
    {
        var teams = YahooPasteParser.Parse(Input()).Snapshot!.Teams;

        Assert.Equal(4, teams.Count);
        Assert.Equal(["Thunder Ducks", "Gridiron Goons", "Couch Commandos", "Red Zone Rejects"], teams.Select(t => t.Name));
        Assert.Equal(["kelly", "dave", "sam", "pat"], teams.Select(t => t.OwnerName));
        Assert.Equal([1, 2, 3, 4], teams.Select(t => t.TeamNumber));
        Assert.Equal("nfl.l.555123.t.1", teams[0].TeamKey);
    }

    [Fact]
    public void Falls_back_to_manager_labels_when_the_table_structure_is_lost()
    {
        var flat = "Thunder Ducks\nManagers kelly\nGridiron Goons\nManagers dave\n";

        var teams = YahooPasteParser.Parse(Input(teams: flat)).Snapshot!.Teams;

        Assert.Equal(2, teams.Count);
        Assert.Equal("Thunder Ducks", teams[0].Name);
        Assert.Equal("kelly", teams[0].OwnerName);
    }

    [Fact]
    public void Maps_pasted_scoring_labels_all_the_way_through_to_scoring_rules()
    {
        var snapshot = YahooPasteParser.Parse(Input()).Snapshot!;
        var scoring = YahooLeagueMapper.Map(snapshot).Request.Scoring
            .ToDictionary(rule => rule.Category, rule => rule.Points);

        // "25 yards per point" is Yahoo's phrasing for 0.04 points per yard.
        Assert.Equal(0.04m, scoring[ScoringCategory.PassingYard]);
        Assert.Equal(0.1m, scoring[ScoringCategory.RushingYard]);
        Assert.Equal(0.1m, scoring[ScoringCategory.ReceivingYard]);
        Assert.Equal(4m, scoring[ScoringCategory.PassingTouchdown]);
        Assert.Equal(-1m, scoring[ScoringCategory.Interception]);
        Assert.Equal(0.5m, scoring[ScoringCategory.Reception]);
        Assert.Equal(-2m, scoring[ScoringCategory.FumbleLost]);
        Assert.Equal(5m, scoring[ScoringCategory.FieldGoal50Plus]);
        Assert.Equal(1m, scoring[ScoringCategory.ExtraPoint]);

        // "Interceptions" (thrown) and "Interception" (D/ST) are different rows.
        Assert.Equal(2m, scoring[ScoringCategory.DefensiveInterception]);
        Assert.Equal(6m, scoring[ScoringCategory.DefensiveTouchdown]);
        Assert.Equal(10m, scoring[ScoringCategory.PointsAllowed0]);
    }

    [Fact]
    public void Does_not_mistake_a_yes_no_setting_for_a_scoring_row()
    {
        var stats = YahooPasteParser.Parse(Input()).Snapshot!.Stats;
        Assert.DoesNotContain(stats, s => s.DisplayName == "fractional points");
    }

    [Fact]
    public void Reports_missing_sections_instead_of_guessing()
    {
        var truncated = "League Name: \tSunday Night\nLeague ID#: \t555123\n";

        var result = YahooPasteParser.Parse(Input(truncated, teams: string.Empty));

        Assert.False(result.Succeeded);
        Assert.Null(result.Snapshot);
        Assert.Contains("Roster Positions", result.MissingSections);
        Assert.Contains("Scoring / stat categories", result.MissingSections);
        Assert.Contains("Teams and managers", result.MissingSections);
    }

    [Fact]
    public void Reports_a_missing_league_id_when_no_url_is_given()
    {
        var settings = SettingsPaste.Replace("League ID#: \t555123\n", string.Empty);
        var result = YahooPasteParser.Parse(new YahooPasteInput
        {
            LeagueUrlOrId = null,
            SettingsText = settings,
            TeamsText = TeamsPaste
        });

        Assert.False(result.Succeeded);
        Assert.Contains("League ID", result.MissingSections);
    }

    [Fact]
    public void Flags_an_auction_league_so_the_mapper_refuses_it()
    {
        var settings = SettingsPaste.Replace("Live Standard Draft", "Live Auction Draft");

        var snapshot = YahooPasteParser.Parse(Input(settings)).Snapshot!;

        Assert.True(snapshot.IsAuction);
        var error = Assert.Throws<InvalidOperationException>(() => YahooLeagueMapper.Map(snapshot));
        Assert.Contains("Auction", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Warns_when_the_team_count_disagrees_with_the_teams_paste()
    {
        var result = YahooPasteParser.Parse(Input());

        // Settings says 12 teams; the Teams paste only carried 4.
        Assert.Contains(result.Warnings, w => w.Contains("12") && w.Contains('4'));
        Assert.Equal(4, result.Snapshot!.TeamCount);
    }

    [Theory]
    [InlineData("https://football.fantasysports.yahoo.com/f1/555123", "555123")]
    [InlineData("https://football.fantasysports.yahoo.com/f1/555123/settings", "555123")]
    [InlineData("nfl.l.555123", "555123")]
    [InlineData("555123", "555123")]
    [InlineData("League ID# 555123", "555123")]
    [InlineData("", null)]
    [InlineData("not a league", null)]
    public void Extracts_the_league_id_from_the_shapes_a_user_might_paste(string input, string? expected)
    {
        Assert.Equal(expected, YahooPasteParser.ExtractLeagueId(input));
    }

    [Theory]
    [InlineData("25 yards per point", 0.04)]
    [InlineData("10 yards per point", 0.1)]
    [InlineData("1 point per 25 yards", 0.04)]
    [InlineData("4", 4)]
    [InlineData("-2", -2)]
    [InlineData("0.5", 0.5)]
    public void Converts_yahoos_scoring_phrasing_into_points_per_unit(string text, double expected)
    {
        Assert.Equal((decimal)expected, YahooPasteParser.ParseStatValue(text));
    }

    [Fact]
    public void Leaves_an_unknown_scoring_label_unmapped_rather_than_guessing()
    {
        var snapshot = YahooPasteParser.Parse(Input()).Snapshot! with
        {
            Stats = [new YahooStatModifier { StatId = 0, Value = 3m, DisplayName = "tackles for loss" }]
        };

        var mapped = YahooLeagueMapper.Map(snapshot);

        Assert.Contains(mapped.UnmappedScoring, entry => entry.Contains("tackles for loss"));
        Assert.Contains(mapped.ReviewItems, item => item.Contains("scoring", StringComparison.OrdinalIgnoreCase));
    }
}
