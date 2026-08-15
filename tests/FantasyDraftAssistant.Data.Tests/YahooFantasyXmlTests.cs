using FantasyDraftAssistant.Providers.Yahoo;

namespace FantasyDraftAssistant.Data.Tests;

public class YahooFantasyXmlTests
{
    [Fact]
    public void Parses_league_list_and_settings_payloads()
    {
        var listed = YahooFantasyXml.ParseLeagueList(ListXml);
        var league = Assert.Single(listed);
        Assert.Equal("461.l.12345", league.LeagueKey);
        Assert.Equal("Gridiron Club", league.Name);
        Assert.Equal(2026, league.Season);
        Assert.Equal(10, league.TeamCount);
        Assert.False(league.IsAuction);
        Assert.True(league.IsKeeper);

        var settings = YahooFantasyXml.ParseLeague(SettingsXml);
        Assert.Contains(settings.Roster, r => r.Position == "Q/W/R/T" && r.Count == 1);
        Assert.Contains(settings.Stats, s => s.StatId == 11 && s.Value == 1.0m);

        var teams = YahooFantasyXml.ParseLeague(TeamsXml);
        Assert.Equal(2, teams.Teams.Count);
        Assert.Equal("Blue Steel", teams.Teams.Single(t => t.IsCurrentUser).Name);
        Assert.Equal("Pat", teams.Teams.Single(t => t.IsCurrentUser).OwnerName);

        var merged = YahooFantasyXml.Merge(settings, teams);
        Assert.Equal("461.l.12345", merged.LeagueKey);
        Assert.Equal(2, merged.Teams.Count);
        Assert.NotEmpty(merged.Roster);
    }

    [Fact]
    public void Rejects_empty_and_non_xml()
    {
        Assert.Throws<InvalidOperationException>(() => YahooFantasyXml.ParseLeague(""));
        Assert.Throws<InvalidOperationException>(() => YahooFantasyXml.ParseLeague("not xml"));
    }

    private const string ListXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <fantasy_content xmlns="http://fantasysports.yahooapis.com/fantasy/v2/base.rng">
          <users>
            <user>
              <games>
                <game>
                  <leagues>
                    <league>
                      <league_key>461.l.12345</league_key>
                      <name>Gridiron Club</name>
                      <season>2026</season>
                      <num_teams>10</num_teams>
                      <is_auction_draft>0</is_auction_draft>
                      <is_keeper>1</is_keeper>
                    </league>
                  </leagues>
                </game>
              </games>
            </user>
          </users>
        </fantasy_content>
        """;

    private const string SettingsXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <fantasy_content xmlns="http://fantasysports.yahooapis.com/fantasy/v2/base.rng">
          <league>
            <league_key>461.l.12345</league_key>
            <name>Gridiron Club</name>
            <season>2026</season>
            <num_teams>10</num_teams>
            <settings>
              <is_auction_draft>0</is_auction_draft>
              <is_keeper>1</is_keeper>
              <draft_type>live</draft_type>
              <draft_rounds>16</draft_rounds>
              <roster_positions>
                <roster_position>
                  <position>QB</position>
                  <count>1</count>
                </roster_position>
                <roster_position>
                  <position>Q/W/R/T</position>
                  <count>1</count>
                </roster_position>
                <roster_position>
                  <position>BN</position>
                  <count>5</count>
                </roster_position>
              </roster_positions>
              <stat_modifiers>
                <stats>
                  <stat>
                    <stat_id>5</stat_id>
                    <value>6</value>
                  </stat>
                  <stat>
                    <stat_id>11</stat_id>
                    <value>1.0</value>
                  </stat>
                </stats>
              </stat_modifiers>
            </settings>
          </league>
        </fantasy_content>
        """;

    private const string TeamsXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <fantasy_content xmlns="http://fantasysports.yahooapis.com/fantasy/v2/base.rng">
          <league>
            <league_key>461.l.12345</league_key>
            <name>Gridiron Club</name>
            <teams>
              <team>
                <team_key>461.l.12345.t.1</team_key>
                <team_id>1</team_id>
                <name>Red Wave</name>
                <managers>
                  <manager>
                    <nickname>Alex</nickname>
                  </manager>
                </managers>
              </team>
              <team>
                <team_key>461.l.12345.t.2</team_key>
                <team_id>2</team_id>
                <name>Blue Steel</name>
                <managers>
                  <manager>
                    <nickname>Pat</nickname>
                    <is_current_login>1</is_current_login>
                  </manager>
                </managers>
              </team>
            </teams>
          </league>
        </fantasy_content>
        """;
}
