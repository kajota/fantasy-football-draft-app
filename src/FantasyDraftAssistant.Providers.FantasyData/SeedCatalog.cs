using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Providers.FantasyData;

internal static class SeedCatalog
{
    internal sealed record SeedPlayer(
        int Rank,
        string Name,
        string Team,
        PlayerPosition Position,
        int Bye,
        double Adp,
        int Tier,
        double PassYds,
        double PassTd,
        double Ints,
        double RushYds,
        double RushTd,
        double Rec,
        double RecYds,
        double RecTd);

    public static IReadOnlyList<SeedPlayer> All { get; } =
    [
        Qb("Josh Allen", "BUF", 7, 1, 12.0, 1, 4100, 30, 10, 520, 8),
        Qb("Lamar Jackson", "BAL", 7, 2, 18.0, 1, 3700, 28, 7, 850, 5),
        Qb("Jalen Hurts", "PHI", 5, 3, 24.0, 1, 3600, 24, 9, 620, 12),
        Qb("Jayden Daniels", "WAS", 12, 4, 30.0, 1, 3800, 26, 9, 780, 6),
        Qb("Joe Burrow", "CIN", 10, 5, 36.0, 1, 4500, 36, 10, 180, 2),
        Qb("Patrick Mahomes", "KC", 6, 6, 48.0, 2, 4300, 32, 11, 350, 2),
        Qb("Baker Mayfield", "TB", 11, 7, 60.0, 2, 4100, 30, 12, 220, 2),
        Qb("Bo Nix", "DEN", 14, 8, 72.0, 2, 3900, 28, 10, 400, 4),
        Qb("Justin Herbert", "LAC", 5, 9, 84.0, 2, 4200, 28, 9, 260, 2),
        Qb("Kyler Murray", "ARI", 8, 10, 90.0, 2, 3700, 24, 10, 520, 5),
        Qb("Brock Purdy", "SF", 14, 11, 96.0, 2, 4100, 26, 11, 220, 3),
        Qb("Dak Prescott", "DAL", 7, 12, 108.0, 3, 4000, 28, 12, 200, 2),
        Qb("Caleb Williams", "CHI", 5, 13, 114.0, 3, 3700, 24, 11, 380, 3),
        Qb("Drake Maye", "NE", 14, 14, 120.0, 3, 3600, 22, 12, 420, 3),
        Qb("Jordan Love", "GB", 10, 15, 126.0, 3, 3900, 26, 11, 180, 2),
        Qb("C.J. Stroud", "HOU", 6, 16, 132.0, 3, 4000, 24, 10, 200, 2),
        Qb("Trevor Lawrence", "JAX", 8, 17, 144.0, 3, 3800, 22, 12, 260, 3),
        Qb("Jared Goff", "DET", 8, 18, 150.0, 3, 4300, 28, 10, 60, 1),
        Qb("Tua Tagovailoa", "MIA", 6, 19, 156.0, 4, 4000, 24, 11, 80, 0),
        Qb("Justin Fields", "NYJ", 9, 20, 168.0, 4, 2800, 18, 9, 700, 6),
        Rb("Bijan Robinson", "ATL", 5, 1, 1.4, 1, 1400, 10, 55, 480, 3),
        Rb("Saquon Barkley", "PHI", 5, 2, 2.2, 1, 1500, 11, 40, 320, 2),
        Rb("Jahmyr Gibbs", "DET", 8, 3, 3.1, 1, 1200, 10, 60, 500, 3),
        Rb("Ashton Jeanty", "LV", 8, 4, 4.8, 1, 1250, 9, 45, 360, 2),
        Rb("De'Von Achane", "MIA", 6, 5, 6.5, 1, 1100, 8, 65, 520, 4),
        Rb("Derrick Henry", "BAL", 7, 6, 8.2, 1, 1400, 13, 18, 140, 1),
        Rb("Bucky Irving", "TB", 11, 7, 10.5, 1, 1150, 8, 50, 400, 2),
        Rb("Josh Jacobs", "GB", 10, 8, 13.0, 2, 1200, 10, 32, 260, 1),
        Rb("Kyren Williams", "LAR", 8, 9, 15.0, 2, 1180, 11, 28, 220, 1),
        Rb("Jonathan Taylor", "IND", 11, 10, 16.5, 2, 1250, 10, 25, 200, 1),
        Rb("Breece Hall", "NYJ", 9, 11, 20.0, 2, 1050, 7, 48, 390, 2),
        Rb("Kenneth Walker III", "SEA", 8, 12, 26.0, 2, 1100, 9, 22, 180, 1),
        Rb("James Cook", "BUF", 7, 13, 28.0, 2, 1080, 8, 38, 320, 2),
        Rb("Chase Brown", "CIN", 10, 14, 32.0, 2, 1000, 8, 45, 350, 2),
        Rb("Alvin Kamara", "NO", 11, 15, 38.0, 2, 850, 6, 70, 520, 3),
        Rb("Chuba Hubbard", "CAR", 14, 16, 42.0, 3, 1050, 7, 35, 260, 1),
        Rb("James Conner", "ARI", 8, 17, 46.0, 3, 980, 8, 30, 230, 1),
        Rb("Joe Mixon", "HOU", 6, 18, 50.0, 3, 1000, 8, 28, 210, 1),
        Rb("David Montgomery", "DET", 8, 19, 54.0, 3, 900, 10, 20, 160, 1),
        Rb("Tony Pollard", "TEN", 10, 20, 62.0, 3, 950, 6, 32, 240, 1),
        Rb("Isiah Pacheco", "KC", 6, 21, 66.0, 3, 900, 7, 28, 220, 1),
        Rb("D'Andre Swift", "CHI", 5, 22, 70.0, 3, 880, 6, 40, 310, 2),
        Rb("Aaron Jones", "MIN", 6, 23, 74.0, 3, 820, 5, 42, 330, 2),
        Rb("Rhamondre Stevenson", "NE", 14, 24, 80.0, 3, 860, 6, 36, 270, 1),
        Rb("Tyrone Tracy Jr.", "NYG", 14, 25, 86.0, 4, 820, 5, 34, 250, 1),
        Rb("Travis Etienne", "JAX", 8, 26, 92.0, 4, 800, 5, 38, 280, 1),
        Rb("Najee Harris", "LAC", 5, 27, 98.0, 4, 850, 6, 22, 160, 1),
        Rb("J.K. Dobbins", "DEN", 14, 28, 104.0, 4, 780, 6, 18, 140, 1),
        Wr("Ja'Marr Chase", "CIN", 10, 1, 3.8, 1, 120, 1550, 12),
        Wr("Justin Jefferson", "MIN", 6, 2, 5.2, 1, 115, 1480, 9),
        Wr("CeeDee Lamb", "DAL", 7, 3, 7.0, 1, 118, 1450, 10),
        Wr("Puka Nacua", "LAR", 8, 4, 9.5, 1, 110, 1400, 8),
        Wr("Nico Collins", "HOU", 6, 5, 11.0, 1, 95, 1320, 9),
        Wr("Amon-Ra St. Brown", "DET", 8, 6, 12.5, 1, 125, 1380, 9),
        Wr("Brian Thomas Jr.", "JAX", 8, 7, 14.0, 1, 90, 1280, 8),
        Wr("Malik Nabers", "NYG", 14, 8, 17.0, 1, 105, 1300, 8),
        Wr("A.J. Brown", "PHI", 5, 9, 21.0, 2, 88, 1250, 8),
        Wr("Drake London", "ATL", 5, 10, 22.5, 2, 100, 1260, 8),
        Wr("Tee Higgins", "CIN", 10, 11, 27.0, 2, 80, 1180, 9),
        Wr("Ladd McConkey", "LAC", 5, 12, 29.0, 2, 92, 1200, 7),
        Wr("Tyreek Hill", "MIA", 6, 13, 33.0, 2, 85, 1220, 8),
        Wr("Garrett Wilson", "NYJ", 9, 14, 35.0, 2, 98, 1190, 6),
        Wr("Jaxon Smith-Njigba", "SEA", 8, 15, 39.0, 2, 95, 1210, 7),
        Wr("Mike Evans", "TB", 11, 16, 41.0, 2, 78, 1150, 10),
        Wr("DK Metcalf", "PIT", 5, 17, 45.0, 2, 75, 1120, 8),
        Wr("Terry McLaurin", "WAS", 12, 18, 47.0, 2, 82, 1140, 7),
        Wr("Davante Adams", "LAR", 8, 19, 51.0, 3, 80, 1080, 8),
        Wr("Marvin Harrison Jr.", "ARI", 8, 20, 55.0, 3, 78, 1100, 7),
        Wr("Courtland Sutton", "DEN", 14, 21, 58.0, 3, 76, 1070, 7),
        Wr("Chris Olave", "NO", 11, 22, 61.0, 3, 85, 1090, 6),
        Wr("DJ Moore", "CHI", 5, 23, 64.0, 3, 80, 1050, 6),
        Wr("DeVonta Smith", "PHI", 5, 24, 68.0, 3, 82, 1060, 6),
        Wr("Zay Flowers", "BAL", 7, 25, 71.0, 3, 84, 1040, 6),
        Wr("Jaylen Waddle", "MIA", 6, 26, 75.0, 3, 78, 1020, 6),
        Wr("Jameson Williams", "DET", 8, 27, 79.0, 3, 60, 1010, 8),
        Wr("George Pickens", "DAL", 7, 28, 82.0, 3, 70, 1030, 7),
        Wr("Xavier Worthy", "KC", 6, 29, 88.0, 3, 62, 900, 7),
        Wr("Rome Odunze", "CHI", 5, 30, 94.0, 4, 72, 980, 6),
        Wr("Jerry Jeudy", "CLE", 9, 31, 100.0, 4, 76, 990, 5),
        Wr("Calvin Ridley", "TEN", 10, 32, 106.0, 4, 70, 970, 6),
        Wr("Tetairoa McMillan", "CAR", 14, 33, 110.0, 4, 74, 960, 6),
        Wr("Chris Godwin", "TB", 11, 34, 116.0, 4, 80, 950, 5),
        Te("Brock Bowers", "LV", 8, 1, 19.0, 1, 95, 1050, 7),
        Te("Trey McBride", "ARI", 8, 2, 23.0, 1, 100, 1020, 6),
        Te("George Kittle", "SF", 14, 3, 34.0, 1, 75, 980, 8),
        Te("Sam LaPorta", "DET", 8, 4, 44.0, 2, 80, 900, 7),
        Te("Travis Kelce", "KC", 6, 5, 52.0, 2, 82, 880, 6),
        Te("T.J. Hockenson", "MIN", 6, 6, 76.0, 2, 78, 820, 5),
        Te("Mark Andrews", "BAL", 7, 7, 85.0, 2, 60, 760, 7),
        Te("David Njoku", "CLE", 9, 8, 93.0, 3, 70, 740, 5),
        Te("Evan Engram", "DEN", 14, 9, 101.0, 3, 75, 720, 4),
        Te("Dallas Goedert", "PHI", 5, 10, 112.0, 3, 62, 680, 5),
        Te("Jake Ferguson", "DAL", 7, 11, 122.0, 3, 65, 650, 5),
        Te("Tucker Kraft", "GB", 10, 12, 130.0, 3, 55, 640, 5),
        K("Justin Tucker", "BAL", 7, 1, 140.0, 5),
        K("Brandon Aubrey", "DAL", 7, 2, 145.0, 5),
        K("Harrison Butker", "KC", 6, 3, 150.0, 5),
        K("Jake Bates", "DET", 8, 4, 155.0, 5),
        K("Younghoe Koo", "ATL", 5, 5, 160.0, 5),
        K("Cameron Dicker", "LAC", 5, 6, 165.0, 5),
        Def("Baltimore", "BAL", 7, 1, 142.0, 5),
        Def("Philadelphia", "PHI", 5, 2, 148.0, 5),
        Def("San Francisco", "SF", 14, 3, 154.0, 5),
        Def("Pittsburgh", "PIT", 5, 4, 159.0, 5),
        Def("Buffalo", "BUF", 7, 5, 164.0, 5),
        Def("Denver", "DEN", 14, 6, 170.0, 5)
    ];

    public static (List<Player> Players, List<PlayerRanking> Rankings, List<PlayerAdp> Adp, List<PlayerProjection> Projections, List<PlayerProviderId> Ids)
        Materialize(string sourceKey)
    {
        var now = DateTimeOffset.UtcNow;
        var players = new List<Player>();
        var rankings = new List<PlayerRanking>();
        var adp = new List<PlayerAdp>();
        var projections = new List<PlayerProjection>();
        var ids = new List<PlayerProviderId>();

        foreach (var seed in All)
        {
            var playerId = PlayerId.FromName(seed.Name, seed.Team, seed.Position.ToString());
            players.Add(new Player
            {
                PlayerId = playerId,
                Name = seed.Name,
                NflTeam = seed.Team,
                PrimaryPosition = seed.Position,
                EligiblePositions = [seed.Position],
                ByeWeek = seed.Bye,
                Status = PlayerStatus.Active,
                StatusUpdatedAt = now
            });
            rankings.Add(new PlayerRanking
            {
                PlayerId = playerId,
                SourceKey = sourceKey,
                OverallRank = seed.Rank,
                PositionRank = seed.Rank,
                Tier = seed.Tier,
                CachedAt = now,
                SourceTimestamp = now
            });
            adp.Add(new PlayerAdp
            {
                PlayerId = playerId,
                SourceKey = sourceKey,
                OverallAdp = seed.Adp,
                CachedAt = now,
                SourceTimestamp = now
            });
            projections.Add(new PlayerProjection
            {
                PlayerId = playerId,
                SourceKey = sourceKey,
                PassingYards = seed.PassYds,
                PassingTouchdowns = seed.PassTd,
                Interceptions = seed.Ints,
                RushingYards = seed.RushYds,
                RushingTouchdowns = seed.RushTd,
                Receptions = seed.Rec,
                ReceivingYards = seed.RecYds,
                ReceivingTouchdowns = seed.RecTd,
                CachedAt = now,
                SourceTimestamp = now
            });
            ids.Add(new PlayerProviderId
            {
                PlayerId = playerId,
                ProviderKey = sourceKey,
                ExternalId = $"{seed.Position}-{seed.Name}".Replace(' ', '-').ToLowerInvariant()
            });
        }

        // Position ranks should be per-position, not overall.
        foreach (var group in rankings.GroupBy(r => All.First(s => PlayerId.FromName(s.Name, s.Team, s.Position.ToString()).Equals(r.PlayerId)).Position))
        {
            var i = 1;
            foreach (var ranking in group.OrderBy(r => r.OverallRank).ToList())
            {
                var idx = rankings.FindIndex(r => r.PlayerId.Equals(ranking.PlayerId));
                rankings[idx] = new PlayerRanking
                {
                    PlayerId = ranking.PlayerId,
                    SourceKey = ranking.SourceKey,
                    OverallRank = ranking.OverallRank,
                    PositionRank = i++,
                    Tier = ranking.Tier,
                    CachedAt = ranking.CachedAt,
                    SourceTimestamp = ranking.SourceTimestamp
                };
            }
        }

        // Overall ranks should be ADP order for a usable board.
        var ordered = All.OrderBy(s => s.Adp).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var seed = ordered[i];
            var playerId = PlayerId.FromName(seed.Name, seed.Team, seed.Position.ToString());
            var idx = rankings.FindIndex(r => r.PlayerId.Equals(playerId));
            var current = rankings[idx];
            rankings[idx] = new PlayerRanking
            {
                PlayerId = current.PlayerId,
                SourceKey = current.SourceKey,
                OverallRank = i + 1,
                PositionRank = current.PositionRank,
                Tier = current.Tier,
                CachedAt = current.CachedAt,
                SourceTimestamp = current.SourceTimestamp
            };
        }

        return (players, rankings, adp, projections, ids);
    }

    private static SeedPlayer Qb(string name, string team, int bye, int rank, double adp, int tier, double passYds, double passTd, double ints, double rushYds, double rushTd) =>
        new(rank, name, team, PlayerPosition.QB, bye, adp, tier, passYds, passTd, ints, rushYds, rushTd, 0, 0, 0);

    private static SeedPlayer Rb(string name, string team, int bye, int rank, double adp, int tier, double rushYds, double rushTd, double rec, double recYds, double recTd) =>
        new(rank, name, team, PlayerPosition.RB, bye, adp, tier, 0, 0, 0, rushYds, rushTd, rec, recYds, recTd);

    private static SeedPlayer Wr(string name, string team, int bye, int rank, double adp, int tier, double rec, double recYds, double recTd) =>
        new(rank, name, team, PlayerPosition.WR, bye, adp, tier, 0, 0, 0, 0, 0, rec, recYds, recTd);

    private static SeedPlayer Te(string name, string team, int bye, int rank, double adp, int tier, double rec, double recYds, double recTd) =>
        new(rank, name, team, PlayerPosition.TE, bye, adp, tier, 0, 0, 0, 0, 0, rec, recYds, recTd);

    private static SeedPlayer K(string name, string team, int bye, int rank, double adp, int tier) =>
        new(rank, name, team, PlayerPosition.K, bye, adp, tier, 0, 0, 0, 0, 0, 0, 0, 0);

    private static SeedPlayer Def(string name, string team, int bye, int rank, double adp, int tier) =>
        new(rank, name, team, PlayerPosition.DEF, bye, adp, tier, 0, 0, 0, 0, 0, 0, 0, 0);
}
