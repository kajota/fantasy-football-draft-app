using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class FantasyDataWriter(SqliteConnectionFactory factory) : IFantasyDataWriter
{
    public Task<FantasyDataRefreshResult> WriteAsync(
        string providerKey,
        IReadOnlyList<Player> players,
        IReadOnlyList<PlayerProviderId> providerIds,
        IReadOnlyList<PlayerRanking> rankings,
        IReadOnlyList<PlayerAdp> adp,
        IReadOnlyList<PlayerProjection> projections,
        CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        var now = DateTimeOffset.UtcNow.ToString("O");

        using (var cmd = db.Cmd("INSERT OR REPLACE INTO FantasyDataProviders(ProviderKey, DisplayName) VALUES ($k, $n);", tx)
                   .Bind("$k", providerKey).Bind("$n", providerKey))
            cmd.ExecuteNonQuery();

        foreach (var player in players)
        {
            using (var cmd = db.Cmd("""
                INSERT INTO Players(PlayerId, Name, NflTeam, PrimaryPosition, ByeWeek, Status, StatusUpdatedAt)
                VALUES ($id, $name, $team, $pos, $bye, $status, $updated)
                ON CONFLICT(PlayerId) DO UPDATE SET
                    Name = excluded.Name,
                    NflTeam = excluded.NflTeam,
                    PrimaryPosition = excluded.PrimaryPosition,
                    ByeWeek = excluded.ByeWeek,
                    Status = excluded.Status,
                    StatusUpdatedAt = excluded.StatusUpdatedAt;
                """, tx)
                       .Bind("$id", player.PlayerId.ToString())
                       .Bind("$name", player.Name)
                       .Bind("$team", player.NflTeam)
                       .Bind("$pos", player.PrimaryPosition.ToString())
                       .Bind("$bye", player.ByeWeek)
                       .Bind("$status", player.Status.ToString())
                       .Bind("$updated", player.StatusUpdatedAt?.ToString("O")))
            {
                cmd.ExecuteNonQuery();
            }

            using (var del = db.Cmd("DELETE FROM PlayerEligiblePositions WHERE PlayerId = $id;", tx)
                       .Bind("$id", player.PlayerId.ToString()))
                del.ExecuteNonQuery();

            foreach (var position in player.EligiblePositions.Distinct())
            {
                using var cmd = db.Cmd("INSERT INTO PlayerEligiblePositions(PlayerId, Position) VALUES ($id, $p);", tx)
                    .Bind("$id", player.PlayerId.ToString())
                    .Bind("$p", position.ToString());
                cmd.ExecuteNonQuery();
            }
        }

        foreach (var mapping in providerIds)
        {
            using var cmd = db.Cmd("""
                INSERT INTO PlayerProviderIds(PlayerId, ProviderKey, ExternalId)
                VALUES ($p, $k, $e)
                ON CONFLICT(PlayerId, ProviderKey) DO UPDATE SET ExternalId = excluded.ExternalId;
                """, tx)
                .Bind("$p", mapping.PlayerId.ToString())
                .Bind("$k", mapping.ProviderKey)
                .Bind("$e", mapping.ExternalId);
            cmd.ExecuteNonQuery();
        }

        foreach (var ranking in rankings)
        {
            using (var src = db.Cmd("INSERT OR IGNORE INTO RankingSources(SourceKey, DisplayName) VALUES ($k, $n);", tx)
                       .Bind("$k", ranking.SourceKey).Bind("$n", ranking.SourceKey))
                src.ExecuteNonQuery();
            using var cmd = db.Cmd("""
                INSERT INTO PlayerRankings(PlayerId, SourceKey, OverallRank, PositionRank, Tier, SourceTimestamp, CachedAt)
                VALUES ($p, $s, $r, $pr, $t, $src, $c)
                ON CONFLICT(PlayerId, SourceKey) DO UPDATE SET
                    OverallRank = excluded.OverallRank,
                    PositionRank = excluded.PositionRank,
                    Tier = excluded.Tier,
                    SourceTimestamp = excluded.SourceTimestamp,
                    CachedAt = excluded.CachedAt;
                """, tx)
                .Bind("$p", ranking.PlayerId.ToString())
                .Bind("$s", ranking.SourceKey)
                .Bind("$r", ranking.OverallRank)
                .Bind("$pr", ranking.PositionRank)
                .Bind("$t", ranking.Tier)
                .Bind("$src", ranking.SourceTimestamp?.ToString("O"))
                .Bind("$c", ranking.CachedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        foreach (var row in adp)
        {
            using (var src = db.Cmd("INSERT OR IGNORE INTO AdpSources(SourceKey, DisplayName) VALUES ($k, $n);", tx)
                       .Bind("$k", row.SourceKey).Bind("$n", row.SourceKey))
                src.ExecuteNonQuery();
            using var cmd = db.Cmd("""
                INSERT INTO PlayerAdp(PlayerId, SourceKey, OverallAdp, SourceTimestamp, CachedAt)
                VALUES ($p, $s, $a, $src, $c)
                ON CONFLICT(PlayerId, SourceKey) DO UPDATE SET
                    OverallAdp = excluded.OverallAdp,
                    SourceTimestamp = excluded.SourceTimestamp,
                    CachedAt = excluded.CachedAt;
                """, tx)
                .Bind("$p", row.PlayerId.ToString())
                .Bind("$s", row.SourceKey)
                .Bind("$a", row.OverallAdp)
                .Bind("$src", row.SourceTimestamp?.ToString("O"))
                .Bind("$c", row.CachedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        foreach (var row in projections)
        {
            using (var src = db.Cmd("INSERT OR IGNORE INTO ProjectionSources(SourceKey, DisplayName) VALUES ($k, $n);", tx)
                       .Bind("$k", row.SourceKey).Bind("$n", row.SourceKey))
                src.ExecuteNonQuery();
            using var cmd = db.Cmd("""
                INSERT INTO PlayerProjections(
                    PlayerId, SourceKey, PassingAttempts, Completions, PassingYards, PassingTouchdowns, Interceptions,
                    RushingAttempts, RushingYards, RushingTouchdowns, Targets, Receptions, ReceivingYards, ReceivingTouchdowns,
                    SourceTimestamp, CachedAt)
                VALUES ($p, $s, $pa, $cmp, $py, $ptd, $int, $ra, $ry, $rtd, $tgt, $rec, $rey, $rtd2, $src, $c)
                ON CONFLICT(PlayerId, SourceKey) DO UPDATE SET
                    PassingAttempts = excluded.PassingAttempts,
                    Completions = excluded.Completions,
                    PassingYards = excluded.PassingYards,
                    PassingTouchdowns = excluded.PassingTouchdowns,
                    Interceptions = excluded.Interceptions,
                    RushingAttempts = excluded.RushingAttempts,
                    RushingYards = excluded.RushingYards,
                    RushingTouchdowns = excluded.RushingTouchdowns,
                    Targets = excluded.Targets,
                    Receptions = excluded.Receptions,
                    ReceivingYards = excluded.ReceivingYards,
                    ReceivingTouchdowns = excluded.ReceivingTouchdowns,
                    SourceTimestamp = excluded.SourceTimestamp,
                    CachedAt = excluded.CachedAt;
                """, tx)
                .Bind("$p", row.PlayerId.ToString())
                .Bind("$s", row.SourceKey)
                .Bind("$pa", row.PassingAttempts)
                .Bind("$cmp", row.Completions)
                .Bind("$py", row.PassingYards)
                .Bind("$ptd", row.PassingTouchdowns)
                .Bind("$int", row.Interceptions)
                .Bind("$ra", row.RushingAttempts)
                .Bind("$ry", row.RushingYards)
                .Bind("$rtd", row.RushingTouchdowns)
                .Bind("$tgt", row.Targets)
                .Bind("$rec", row.Receptions)
                .Bind("$rey", row.ReceivingYards)
                .Bind("$rtd2", row.ReceivingTouchdowns)
                .Bind("$src", row.SourceTimestamp?.ToString("O"))
                .Bind("$c", row.CachedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        RecordRefresh(db, tx, providerKey, "players", players.Count, now);
        RecordRefresh(db, tx, providerKey, "rankings", rankings.Count, now);
        RecordRefresh(db, tx, providerKey, "adp", adp.Count, now);
        RecordRefresh(db, tx, providerKey, "projections", projections.Count, now);
        tx.Commit();

        return Task.FromResult(new FantasyDataRefreshResult
        {
            Succeeded = true,
            PlayersWritten = players.Count,
            RankingsWritten = rankings.Count,
            AdpWritten = adp.Count,
            ProjectionsWritten = projections.Count,
            RefreshedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<IReadOnlyDictionary<PlayerId, PlayerRanking>> GetRankingsAsync(string? sourceKey = null, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd(sourceKey is null
            ? "SELECT * FROM PlayerRankings;"
            : "SELECT * FROM PlayerRankings WHERE SourceKey = $s;").Bind("$s", sourceKey);
        using var reader = cmd.ExecuteReader();
        var map = new Dictionary<PlayerId, PlayerRanking>();
        while (reader.Read())
        {
            var row = new PlayerRanking
            {
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                SourceKey = reader.GetString(reader.GetOrdinal("SourceKey")),
                OverallRank = reader.GetInt32(reader.GetOrdinal("OverallRank")),
                PositionRank = reader.GetNullInt(reader.GetOrdinal("PositionRank")),
                Tier = reader.GetNullInt(reader.GetOrdinal("Tier")),
                SourceTimestamp = reader.GetNullTime(reader.GetOrdinal("SourceTimestamp")),
                CachedAt = reader.GetTime(reader.GetOrdinal("CachedAt"))
            };
            map[row.PlayerId] = row;
        }

        return Task.FromResult<IReadOnlyDictionary<PlayerId, PlayerRanking>>(map);
    }

    public Task<IReadOnlyDictionary<PlayerId, PlayerAdp>> GetAdpAsync(string? sourceKey = null, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd(sourceKey is null
            ? "SELECT * FROM PlayerAdp;"
            : "SELECT * FROM PlayerAdp WHERE SourceKey = $s;").Bind("$s", sourceKey);
        using var reader = cmd.ExecuteReader();
        var map = new Dictionary<PlayerId, PlayerAdp>();
        while (reader.Read())
        {
            var row = new PlayerAdp
            {
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                SourceKey = reader.GetString(reader.GetOrdinal("SourceKey")),
                OverallAdp = reader.GetDouble(reader.GetOrdinal("OverallAdp")),
                SourceTimestamp = reader.GetNullTime(reader.GetOrdinal("SourceTimestamp")),
                CachedAt = reader.GetTime(reader.GetOrdinal("CachedAt"))
            };
            map[row.PlayerId] = row;
        }

        return Task.FromResult<IReadOnlyDictionary<PlayerId, PlayerAdp>>(map);
    }

    public Task<IReadOnlyDictionary<PlayerId, PlayerProjection>> GetProjectionsAsync(string? sourceKey = null, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd(sourceKey is null
            ? "SELECT * FROM PlayerProjections;"
            : "SELECT * FROM PlayerProjections WHERE SourceKey = $s;").Bind("$s", sourceKey);
        using var reader = cmd.ExecuteReader();
        var map = new Dictionary<PlayerId, PlayerProjection>();
        while (reader.Read())
        {
            var row = new PlayerProjection
            {
                PlayerId = PlayerId.Parse(reader.GetString(reader.GetOrdinal("PlayerId"))),
                SourceKey = reader.GetString(reader.GetOrdinal("SourceKey")),
                PassingAttempts = reader.GetDouble(reader.GetOrdinal("PassingAttempts")),
                Completions = reader.GetDouble(reader.GetOrdinal("Completions")),
                PassingYards = reader.GetDouble(reader.GetOrdinal("PassingYards")),
                PassingTouchdowns = reader.GetDouble(reader.GetOrdinal("PassingTouchdowns")),
                Interceptions = reader.GetDouble(reader.GetOrdinal("Interceptions")),
                RushingAttempts = reader.GetDouble(reader.GetOrdinal("RushingAttempts")),
                RushingYards = reader.GetDouble(reader.GetOrdinal("RushingYards")),
                RushingTouchdowns = reader.GetDouble(reader.GetOrdinal("RushingTouchdowns")),
                Targets = reader.GetDouble(reader.GetOrdinal("Targets")),
                Receptions = reader.GetDouble(reader.GetOrdinal("Receptions")),
                ReceivingYards = reader.GetDouble(reader.GetOrdinal("ReceivingYards")),
                ReceivingTouchdowns = reader.GetDouble(reader.GetOrdinal("ReceivingTouchdowns")),
                SourceTimestamp = reader.GetNullTime(reader.GetOrdinal("SourceTimestamp")),
                CachedAt = reader.GetTime(reader.GetOrdinal("CachedAt"))
            };
            map[row.PlayerId] = row;
        }

        return Task.FromResult<IReadOnlyDictionary<PlayerId, PlayerProjection>>(map);
    }

    public Task<IReadOnlyList<FantasyDataRefreshInfo>> GetRefreshInfoAsync(CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("SELECT ProviderKey, Dataset, RefreshedAt, RecordCount FROM FantasyDataRefreshes ORDER BY RefreshedAt DESC;");
        using var reader = cmd.ExecuteReader();
        var list = new List<FantasyDataRefreshInfo>();
        while (reader.Read())
        {
            list.Add(new FantasyDataRefreshInfo
            {
                ProviderKey = reader.GetString(0),
                Dataset = reader.GetString(1),
                RefreshedAt = reader.GetTime(2),
                RecordCount = reader.GetInt32(3)
            });
        }

        return Task.FromResult<IReadOnlyList<FantasyDataRefreshInfo>>(list);
    }

    private static void RecordRefresh(Microsoft.Data.Sqlite.SqliteConnection db, Microsoft.Data.Sqlite.SqliteTransaction tx, string provider, string dataset, int count, string now)
    {
        using var cmd = db.Cmd("""
            INSERT INTO FantasyDataRefreshes(RefreshId, ProviderKey, Dataset, RefreshedAt, RecordCount)
            VALUES ($id, $p, $d, $t, $c);
            """, tx)
            .Bind("$id", Guid.NewGuid().ToString("D"))
            .Bind("$p", provider)
            .Bind("$d", dataset)
            .Bind("$t", now)
            .Bind("$c", count);
        cmd.ExecuteNonQuery();
    }
}
