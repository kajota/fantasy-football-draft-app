using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Providers.FantasyData;

namespace FantasyDraftAssistant.Data.Tests;

public class FantasyProsCatalogTests
{
    [Fact]
    public void Parses_consensus_ranks_tiers_and_adp()
    {
        const string json = """
            {
              "sport": "NFL",
              "type": "Draft HALF",
              "players": [
                {
                  "player_id": 17298,
                  "player_name": "Bijan Robinson",
                  "player_team_id": "ATL",
                  "player_position_id": "RB",
                  "rank_ecr": 1,
                  "pos_rank": "RB1",
                  "tier": 1,
                  "rank_min": "1",
                  "rank_max": "3",
                  "rank_std": "0.53",
                  "adp": 1.4,
                  "yahoo_id": "31002"
                },
                {
                  "player_id": 19276,
                  "player_name": "Josh Allen",
                  "player_team_id": "BUF",
                  "player_position_id": "QB",
                  "rank_ecr": 8,
                  "pos_rank": 1,
                  "tier": 2
                }
              ]
            }
            """;

        var players = FantasyProsCatalog.ParseRankings(json);
        Assert.Equal(2, players.Count);
        var bijan = players[0];
        Assert.Equal("Bijan Robinson", bijan.Name);
        Assert.Equal(PlayerPosition.RB, bijan.Position);
        Assert.Equal(1, bijan.OverallRank);
        Assert.Equal(1, bijan.PositionRank);
        Assert.Equal(1, bijan.Tier);
        Assert.Equal(1, bijan.RankMin);
        Assert.Equal(3, bijan.RankMax);
        Assert.Equal(0.53, bijan.RankStd);
        Assert.Equal(1.4, bijan.Adp);
        Assert.Equal("31002", bijan.YahooId);
        Assert.Equal(1, players[1].PositionRank);
    }

    [Fact]
    public void Parses_adp_list_from_average_rank()
    {
        const string json = """
            {
              "type": "ADP Half PPR",
              "players": [
                {
                  "player_id": 17298,
                  "player_name": "Jahmyr Gibbs",
                  "player_team_id": "DET",
                  "player_position_id": "RB",
                  "rank_ecr": 1,
                  "rank_ave": "1.50"
                }
              ]
            }
            """;

        var adp = FantasyProsCatalog.ParseAdp(json);
        Assert.Equal(1.50, adp["17298"]);
    }

    [Fact]
    public void Parses_wrapped_projections()
    {
        const string json = """
            {
              "data": {
                "players": [
                  {
                    "player_id": "19276",
                    "player_name": "Josh Allen",
                    "stats": {
                      "passing_yds": 3650,
                      "passing_tds": 27,
                      "pass_int": 10,
                      "rush_yd": 535,
                      "rush_td": 11
                    }
                  }
                ]
              }
            }
            """;

        var row = Assert.Single(FantasyProsCatalog.ParseProjections(json));
        Assert.Equal(3650, row.PassingYards);
        Assert.Equal(27, row.PassingTouchdowns);
        Assert.Equal(10, row.Interceptions);
        Assert.Equal(535, row.RushingYards);
    }

    /// <summary>
    /// Verbatim from a live nfl/2026/projections?week=0&amp;position=WR response. The other
    /// projection test uses invented field names that happen to sit in our alias lists, which
    /// is how "rec_rec" went unnoticed and every pass catcher scored as if the league were
    /// non-PPR. Keep this fixture matching the wire format, not our expectations of it.
    /// </summary>
    [Fact]
    public void Parses_the_stat_names_the_api_actually_sends()
    {
        const string json = """
            {
              "players": [
                {
                  "fpid": 23180,
                  "name": "Puka Nacua",
                  "position_id": "WR",
                  "team_id": "LAR",
                  "stats": {
                    "points": 222.8,
                    "points_ppr": 339.8,
                    "points_half": 281.3,
                    "rec_rec": 117,
                    "rec_yds": 1539,
                    "rec_tds": 9,
                    "rush_att": 13.64,
                    "rush_yds": 85,
                    "rush_tds": 1.39,
                    "fumbles": 0.98
                  }
                }
              ]
            }
            """;

        var row = Assert.Single(FantasyProsCatalog.ParseProjections(json));
        Assert.Equal(117, row.Receptions);
        Assert.Equal(1539, row.ReceivingYards);
        Assert.Equal(9, row.ReceivingTouchdowns);
        Assert.Equal(85, row.RushingYards);
        Assert.Equal(1.39, row.RushingTouchdowns);
        Assert.True(row.HasScorableStats);
    }

    [Fact]
    public void A_kicker_row_carries_nothing_this_app_can_score()
    {
        // Kickers report only fga/fg/xpt, so a row of zeros would claim a confident
        // projection of zero points rather than an absent one.
        const string json = """
            {
              "players": [
                {
                  "fpid": 1,
                  "name": "Cam Little",
                  "position_id": "K",
                  "team_id": "JAX",
                  "stats": { "points": 142.1, "fga": 31.2, "fg": 26.1, "xpt": 33.4 }
                }
              ]
            }
            """;

        var row = Assert.Single(FantasyProsCatalog.ParseProjections(json));
        Assert.False(row.HasScorableStats);
    }

    [Fact]
    public void Ignores_non_fantasy_positions()
    {
        const string json = """
            { "players": [ { "player_name": "Some LB", "player_position_id": "LB", "rank_ecr": 12 } ] }
            """;
        Assert.Empty(FantasyProsCatalog.ParseRankings(json));
    }
}
