using System.Text.Json;
using System.Text.Json.Nodes;
using FantasyDraftAssistant.Core.Ids;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Database;

/// <summary>
/// Rebuilds player identity so it no longer includes the NFL team.
///
/// Identity used to be hash(name, team, position). Team is not part of who a player is: the
/// providers disagree about it (FantasyPros says JAC, Sleeper says JAX) and real players change
/// teams mid-season. Every disagreement forked one person into two rows, so the same player
/// appeared twice in the available list with each copy holding half the data.
///
/// This maps every row to hash(name, position), merges the duplicates, and rewrites every
/// reference — including the player ids embedded in DraftEvents payloads, which the event log
/// replays from.
/// </summary>
public sealed class PlayerIdentityMigration : ICodeMigration
{
    public int Version => 12;
    public string Name => "player-identity";
    public bool RequiresBackup => true;

    public void Apply(SqliteConnection connection, SqliteTransaction transaction)
    {
        var remap = BuildRemap(connection, transaction);
        if (remap.Count == 0)
            return;

        WriteMapTable(connection, transaction, remap);

        // Order matters. Several child tables cascade on Players deletes, so every child is
        // staged into a temp table before Players is rewritten, then written back afterwards.
        var staged = StageChildTables(connection, transaction);
        MergePlayers(connection, transaction);
        foreach (var (table, columns) in staged)
            ApplyStaged(connection, transaction, table, columns);

        RewriteSimpleReferences(connection, transaction);
        RewriteEventPayloads(connection, transaction, remap);

        Execute(connection, transaction, "DROP TABLE IF EXISTS _PlayerIdRemap;");
    }

    /// <summary>Old id to new id, for the rows whose id actually changes.</summary>
    private static Dictionary<string, string> BuildRemap(SqliteConnection connection, SqliteTransaction transaction)
    {
        var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = "SELECT PlayerId, Name, PrimaryPosition FROM Players;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var oldId = reader.GetString(0);
            var newId = PlayerId.FromName(reader.GetString(1), reader.GetString(2)).ToString();
            if (!string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
                remap[oldId] = newId;
        }

        return remap;
    }

    private static void WriteMapTable(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Dictionary<string, string> remap)
    {
        Execute(connection, transaction, """
            DROP TABLE IF EXISTS _PlayerIdRemap;
            CREATE TEMP TABLE _PlayerIdRemap (OldId TEXT PRIMARY KEY, NewId TEXT NOT NULL);
            """);

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO _PlayerIdRemap(OldId, NewId) VALUES ($old, $new);";
        var oldParam = insert.Parameters.Add("$old", SqliteType.Text);
        var newParam = insert.Parameters.Add("$new", SqliteType.Text);
        foreach (var (oldId, newId) in remap)
        {
            oldParam.Value = oldId;
            newParam.Value = newId;
            insert.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Collapses duplicate player rows. The survivor is the most recently refreshed row, which
    /// is the live provider rather than the bundled seed data, so the surviving team is the
    /// current one. A real team beats "FA" regardless of age.
    /// </summary>
    private static void MergePlayers(SqliteConnection connection, SqliteTransaction transaction) =>
        Execute(connection, transaction, """
            CREATE TEMP TABLE _PlayersMerged AS
            SELECT NewId AS PlayerId, Name, NflTeam, PrimaryPosition, ByeWeek, Status,
                   StatusUpdatedAt, YearsExp, InjuryBodyPart, InjuryNotes, InjuryStartedOn
            FROM (
                SELECT COALESCE(m.NewId, p.PlayerId) AS NewId, p.*,
                       ROW_NUMBER() OVER (
                           PARTITION BY COALESCE(m.NewId, p.PlayerId)
                           ORDER BY CASE WHEN p.NflTeam = 'FA' THEN 1 ELSE 0 END,
                                    COALESCE(p.StatusUpdatedAt, '') DESC,
                                    p.rowid DESC
                       ) AS rn
                FROM Players p
                LEFT JOIN _PlayerIdRemap m ON m.OldId = p.PlayerId
            )
            WHERE rn = 1;

            DELETE FROM Players;
            INSERT INTO Players (PlayerId, Name, NflTeam, PrimaryPosition, ByeWeek, Status,
                                 StatusUpdatedAt, YearsExp, InjuryBodyPart, InjuryNotes, InjuryStartedOn)
            SELECT PlayerId, Name, NflTeam, PrimaryPosition, ByeWeek, Status,
                   StatusUpdatedAt, YearsExp, InjuryBodyPart, InjuryNotes, InjuryStartedOn
            FROM _PlayersMerged;
            DROP TABLE _PlayersMerged;
            """);

    /// <summary>
    /// Tables keyed partly by PlayerId, where merging two players can collide on the key.
    /// Freshest row wins; for the per-draft availability projection a player counts as drafted
    /// if either copy was, so the minimum survives.
    /// </summary>
    private static List<(string Table, string Columns)> StageChildTables(
        SqliteConnection connection,
        SqliteTransaction transaction)
    {
        List<(string Table, string Columns, string PartitionExtra, string Order, bool GroupBy)> tables =
        [
            ("PlayerEligiblePositions", "PlayerId, Position", "Position", "t.rowid DESC", false),
            ("PlayerProviderIds", "PlayerId, ProviderKey, ExternalId", "ProviderKey", "t.rowid DESC", false),
            ("PlayerStatuses", "PlayerId, Status, StatusUpdatedAt", "",
                "COALESCE(t.StatusUpdatedAt, '') DESC, t.rowid DESC", false),
            ("PlayerRankings",
                "PlayerId, SourceKey, OverallRank, PositionRank, Tier, SourceTimestamp, CachedAt, RankMin, RankMax, RankStd",
                "SourceKey", "t.CachedAt DESC, t.rowid DESC", false),
            ("PlayerProjections",
                "PlayerId, SourceKey, PassingAttempts, Completions, PassingYards, PassingTouchdowns, "
                + "Interceptions, RushingAttempts, RushingYards, RushingTouchdowns, Targets, Receptions, "
                + "ReceivingYards, ReceivingTouchdowns, SourceTimestamp, CachedAt",
                "SourceKey", "t.CachedAt DESC, t.rowid DESC", false),
            ("PlayerAdp", "PlayerId, SourceKey, OverallAdp, SourceTimestamp, CachedAt",
                "SourceKey", "t.CachedAt DESC, t.rowid DESC", false),
            ("PlayerTiers", "PlayerId, SourceKey, Tier", "SourceKey", "t.rowid DESC", false),
            ("TeamRosterProjection", "DraftId, BranchId, TeamId, PlayerId, OverallPick",
                "DraftId, BranchId, TeamId", "t.OverallPick ASC", false),
            ("PlayerAvailabilityProjection", "DraftId, BranchId, PlayerId, MIN(IsAvailable) AS IsAvailable",
                "DraftId, BranchId", "", true)
        ];

        var staged = new List<(string, string)>();
        foreach (var (table, columns, partitionExtra, order, groupBy) in tables)
        {
            Rebuild(connection, transaction, table, columns, partitionExtra, order, groupBy);
            staged.Add((table, columns));
        }

        return staged;
    }

    /// <summary>Empties the real table and refills it from its staged copy.</summary>
    private static void ApplyStaged(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string columns)
    {
        var insertColumns = columns.Replace("MIN(IsAvailable) AS IsAvailable", "IsAvailable");
        Execute(connection, transaction, $"""
            DELETE FROM {table};
            INSERT INTO {table} ({insertColumns}) SELECT * FROM _Stage_{table};
            DROP TABLE _Stage_{table};
            """);
    }

    /// <summary>
    /// Stages one table into a temp copy, mapping ids and keeping a single row per key.
    /// SQLite cannot UPDATE a primary key into a value that already exists, so the table is
    /// rebuilt rather than updated in place.
    /// </summary>
    private static void Rebuild(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string table,
        string columns,
        string partitionExtra,
        string order,
        bool groupBy = false)
    {
        // SQLite cannot see a SELECT alias from the same level inside PARTITION BY, so the
        // mapping expression is repeated there rather than referred to by name.
        const string mapped = "COALESCE(m.NewId, t.PlayerId)";
        var qualifiedExtra = string.Join(", ", partitionExtra
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(column => $"t.{column}"));
        var partition = qualifiedExtra.Length == 0 ? mapped : $"{mapped}, {qualifiedExtra}";
        var projection = columns.Replace("PlayerId", "NewPlayerId");
        // The GROUP BY sits one level out, where NewPlayerId is an ordinary column.
        var grouping = qualifiedExtra.Length == 0
            ? "NewPlayerId"
            : $"NewPlayerId, {partitionExtra}";

        var sql = groupBy
            ? $"""
               CREATE TEMP TABLE _Stage_{table} AS
               SELECT {projection}
               FROM (SELECT {mapped} AS NewPlayerId, t.* FROM {table} t
                     LEFT JOIN _PlayerIdRemap m ON m.OldId = t.PlayerId)
               GROUP BY {grouping};
               """
            : $"""
               CREATE TEMP TABLE _Stage_{table} AS
               SELECT {projection}
               FROM (
                   SELECT {mapped} AS NewPlayerId, t.*,
                          ROW_NUMBER() OVER (PARTITION BY {partition} ORDER BY {order}) AS _rn
                   FROM {table} t
                   LEFT JOIN _PlayerIdRemap m ON m.OldId = t.PlayerId
               )
               WHERE _rn = 1;
               """;

        // Staging only. The real table is not touched until Players has been merged, because
        // several of these cascade-delete when Players rows go.
        Execute(connection, transaction, $"DROP TABLE IF EXISTS _Stage_{table}; {sql}");
    }

    /// <summary>Tables where PlayerId is not part of the key, so a plain update is enough.</summary>
    private static void RewriteSimpleReferences(SqliteConnection connection, SqliteTransaction transaction)
    {
        foreach (var table in new[] { "Keepers", "DraftQueueItems", "ActiveDraftSelections" })
        {
            Execute(connection, transaction, $"""
                UPDATE {table}
                SET PlayerId = (SELECT NewId FROM _PlayerIdRemap WHERE OldId = {table}.PlayerId)
                WHERE PlayerId IN (SELECT OldId FROM _PlayerIdRemap);
                """);
        }
    }

    /// <summary>
    /// The event log is the source of truth a draft replays from, so the ids embedded in its
    /// JSON payloads have to move too. Only the playerId property is touched — a blind string
    /// replace could corrupt an unrelated field that happens to hold the same text.
    /// </summary>
    private static void RewriteEventPayloads(
        SqliteConnection connection,
        SqliteTransaction transaction,
        Dictionary<string, string> remap)
    {
        var updates = new List<(string EventId, string Payload)>();
        using (var read = connection.CreateCommand())
        {
            read.Transaction = transaction;
            read.CommandText = "SELECT EventId, PayloadJson FROM DraftEvents WHERE PayloadJson LIKE '%playerId%';";
            using var reader = read.ExecuteReader();
            while (reader.Read())
            {
                var eventId = reader.GetString(0);
                var payload = reader.GetString(1);
                if (Rewrite(payload, remap) is { } updated)
                    updates.Add((eventId, updated));
            }
        }

        using var write = connection.CreateCommand();
        write.Transaction = transaction;
        write.CommandText = "UPDATE DraftEvents SET PayloadJson = $p WHERE EventId = $id;";
        var payloadParam = write.Parameters.Add("$p", SqliteType.Text);
        var idParam = write.Parameters.Add("$id", SqliteType.Text);
        foreach (var (eventId, payload) in updates)
        {
            payloadParam.Value = payload;
            idParam.Value = eventId;
            write.ExecuteNonQuery();
        }
    }

    /// <summary>Returns the rewritten payload, or null when nothing changed.</summary>
    public static string? Rewrite(string payload, IReadOnlyDictionary<string, string> remap)
    {
        JsonNode? node;
        try
        {
            node = JsonNode.Parse(payload);
        }
        catch (JsonException)
        {
            return null;
        }

        if (node is not JsonObject obj)
            return null;

        var changed = false;
        foreach (var property in obj.ToList())
        {
            if (!property.Key.EndsWith("playerId", StringComparison.OrdinalIgnoreCase))
                continue;
            if (property.Value?.GetValue<string>() is not { } current)
                continue;
            if (!remap.TryGetValue(current, out var replacement))
                continue;

            obj[property.Key] = replacement;
            changed = true;
        }

        return changed ? obj.ToJsonString() : null;
    }

    private static void Execute(SqliteConnection connection, SqliteTransaction transaction, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
