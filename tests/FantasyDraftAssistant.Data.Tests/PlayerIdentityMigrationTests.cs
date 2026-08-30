using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Tests;

/// <summary>
/// Migration 012 rewrites primary keys across thirteen tables and the event log, so it is
/// exercised against a database seeded to look like the real one did: the same player forked
/// into two rows because two providers disagreed about their NFL team.
/// </summary>
public class PlayerIdentityMigrationTests : IDisposable
{
    private readonly string _root;
    private readonly SqliteConnectionFactory _factory;

    private static readonly string JacId = PlayerId.LegacyFromName("Travis Hunter", "JAC", "WR").ToString();
    private static readonly string JaxId = PlayerId.LegacyFromName("Travis Hunter", "JAX", "WR").ToString();
    private static readonly string MergedId = PlayerId.FromName("Travis Hunter", "WR").ToString();

    public PlayerIdentityMigrationTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _factory = new SqliteConnectionFactory(new AppPaths(_root));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private void RunMigrations() => new MigrationRunner(_factory).Apply();

    private SqliteConnection Open()
    {
        var connection = _factory.Open();
        return connection;
    }

    /// <summary>Writes the two forked rows and every reference the real database had.</summary>
    private void SeedForkedPlayer()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"""
            INSERT INTO Players (PlayerId, Name, NflTeam, PrimaryPosition, Status, StatusUpdatedAt)
            VALUES ('{JacId}', 'Travis Hunter', 'JAC', 'WR', 'Active', NULL),
                   ('{JaxId}', 'Travis Hunter', 'JAX', 'WR', 'Questionable', '2026-08-18T10:00:00Z');

            INSERT INTO PlayerEligiblePositions (PlayerId, Position)
            VALUES ('{JacId}', 'WR'), ('{JaxId}', 'WR');

            INSERT INTO PlayerProviderIds (PlayerId, ProviderKey, ExternalId)
            VALUES ('{JacId}', 'yahoo', 'y-1'), ('{JaxId}', 'sleeper', 's-1');

            INSERT INTO PlayerRankings (PlayerId, SourceKey, OverallRank, CachedAt)
            VALUES ('{JacId}', 'fantasypros', 170, '2026-08-01T00:00:00Z'),
                   ('{JaxId}', 'fantasypros', 165, '2026-08-18T00:00:00Z');

            INSERT INTO PlayerAdp (PlayerId, SourceKey, OverallAdp, CachedAt)
            VALUES ('{JacId}', 'fantasypros', 140.5, '2026-08-01T00:00:00Z');
            """;
        cmd.ExecuteNonQuery();
    }

    private static string Scalar(SqliteConnection connection, string sql)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        return cmd.ExecuteScalar()?.ToString() ?? "";
    }

    [Fact]
    public void Forked_player_becomes_one_row_under_the_team_free_id()
    {
        RunMigrations();
        SeedForkedPlayer();
        ResetSchemaVersionTo11();
        RunMigrations();

        using var connection = Open();
        Assert.Equal("1", Scalar(connection, "SELECT COUNT(*) FROM Players WHERE Name = 'Travis Hunter';"));
        Assert.Equal("1", Scalar(connection, $"SELECT COUNT(*) FROM Players WHERE PlayerId = '{MergedId}';"));
        Assert.Equal("0", Scalar(connection, $"SELECT COUNT(*) FROM Players WHERE PlayerId IN ('{JacId}', '{JaxId}');"));
    }

    [Fact]
    public void Surviving_row_keeps_the_most_recently_refreshed_team()
    {
        RunMigrations();
        SeedForkedPlayer();
        ResetSchemaVersionTo11();
        RunMigrations();

        using var connection = Open();
        // Sleeper is the live source and stamps StatusUpdatedAt, so its JAX row wins.
        Assert.Equal("JAX", Scalar(connection, $"SELECT NflTeam FROM Players WHERE PlayerId = '{MergedId}';"));
        Assert.Equal("Questionable", Scalar(connection, $"SELECT Status FROM Players WHERE PlayerId = '{MergedId}';"));
    }

    [Fact]
    public void Provider_ids_from_both_copies_survive()
    {
        RunMigrations();
        SeedForkedPlayer();
        ResetSchemaVersionTo11();
        RunMigrations();

        using var connection = Open();
        Assert.Equal("2", Scalar(connection, $"SELECT COUNT(*) FROM PlayerProviderIds WHERE PlayerId = '{MergedId}';"));
        Assert.Equal("y-1", Scalar(connection,
            $"SELECT ExternalId FROM PlayerProviderIds WHERE PlayerId = '{MergedId}' AND ProviderKey = 'yahoo';"));
        Assert.Equal("s-1", Scalar(connection,
            $"SELECT ExternalId FROM PlayerProviderIds WHERE PlayerId = '{MergedId}' AND ProviderKey = 'sleeper';"));
    }

    [Fact]
    public void Clashing_rankings_keep_the_freshest_row()
    {
        RunMigrations();
        SeedForkedPlayer();
        ResetSchemaVersionTo11();
        RunMigrations();

        using var connection = Open();
        // Both copies had a fantasypros ranking; the primary key allows only one.
        Assert.Equal("1", Scalar(connection, $"SELECT COUNT(*) FROM PlayerRankings WHERE PlayerId = '{MergedId}';"));
        Assert.Equal("165", Scalar(connection, $"SELECT OverallRank FROM PlayerRankings WHERE PlayerId = '{MergedId}';"));
        // Data only one copy had is carried across rather than dropped.
        Assert.Equal("1", Scalar(connection, $"SELECT COUNT(*) FROM PlayerAdp WHERE PlayerId = '{MergedId}';"));
    }

    [Fact]
    public void Draft_selections_and_the_event_log_point_at_the_merged_player()
    {
        RunMigrations();
        SeedForkedPlayer();
        using (var connection = Open())
        {
            using var cmd = connection.CreateCommand();
            var eventPayload = $$"""{"overallPick":1,"teamId":"t1","playerId":"{{JacId}}"}""";
            cmd.CommandText = $"""
                INSERT INTO Leagues (LeagueId, Name, Platform, Season, TeamCount, DraftType,
                                     RoundCount, RosterSize, DraftSourcePreference, CreatedAt)
                VALUES ('l1', 'League', 'Manual', 2026, 12, 'Snake', 16, 16, 'Manual', '2026-08-18T00:00:00Z');

                INSERT INTO Drafts (DraftId, LeagueId, Name, Season, Status, SourceMode, CreatedAt)
                VALUES ('d1', 'l1', 'Draft', 2026, 'InProgress', 'Manual', '2026-08-18T00:00:00Z');

                INSERT INTO ActiveDraftSelections
                    (DraftId, BranchId, OverallPick, EventId, DraftSlotId, Round, RoundPick, TeamId, PlayerId, Source, ObservedAt)
                VALUES ('d1', 'b1', 1, 'e1', 's1', 1, 1, 't1', '{JacId}', 'user', '2026-08-18T00:00:00Z');

                INSERT INTO DraftEvents
                    (EventId, DraftId, BranchId, EventType, StateVersion, SequenceNumber, CreatedAt, CreatedBy, PayloadJson)
                VALUES ('e1', 'd1', 'b1', 'PlayerDrafted', 2, 2, '2026-08-18T00:00:00Z', 'user', '{eventPayload}');
                """;
            cmd.ExecuteNonQuery();
        }

        ResetSchemaVersionTo11();
        RunMigrations();

        using var check = Open();
        Assert.Equal(MergedId, Scalar(check, "SELECT PlayerId FROM ActiveDraftSelections WHERE OverallPick = 1;"));
        var payload = Scalar(check, "SELECT PayloadJson FROM DraftEvents WHERE EventId = 'e1';");
        Assert.Contains(MergedId, payload);
        Assert.DoesNotContain(JacId, payload);
        // Unrelated fields must be untouched.
        Assert.Contains("\"teamId\":\"t1\"", payload);
    }

    [Fact]
    public void Migration_is_idempotent()
    {
        RunMigrations();
        SeedForkedPlayer();
        ResetSchemaVersionTo11();
        RunMigrations();
        ResetSchemaVersionTo11();
        RunMigrations();

        using var connection = Open();
        Assert.Equal("1", Scalar(connection, "SELECT COUNT(*) FROM Players WHERE Name = 'Travis Hunter';"));
        Assert.Equal("1", Scalar(connection, $"SELECT COUNT(*) FROM Players WHERE PlayerId = '{MergedId}';"));
    }

    [Fact]
    public void Payload_rewrite_leaves_unknown_ids_and_malformed_json_alone()
    {
        var remap = new Dictionary<string, string> { ["old"] = "new" };

        Assert.Null(PlayerIdentityMigration.Rewrite("{\"playerId\":\"untouched\"}", remap));
        Assert.Null(PlayerIdentityMigration.Rewrite("not json at all", remap));
        Assert.Null(PlayerIdentityMigration.Rewrite("{\"leagueId\":\"old\"}", remap));

        var rewritten = PlayerIdentityMigration.Rewrite("{\"playerId\":\"old\",\"keep\":\"old\"}", remap);
        Assert.Contains("\"playerId\":\"new\"", rewritten);
        Assert.Contains("\"keep\":\"old\"", rewritten);
    }

    /// <summary>Rewinds only migration 12 so it re-runs against seeded rows; later migrations stay applied.</summary>
    /// <summary>
    /// Rewinds just far enough to replay migration 12 in isolation. Migration 17 (whose
    /// SchemaVersion row is left alone, so it does not rerun) later dropped the two projection
    /// tables migration 12 still expects to find, since those existed at version 11 and weren't
    /// dropped until 17 — so they're recreated here to put the schema back into the shape
    /// migration 12 was written against.
    /// </summary>
    private void ResetSchemaVersionTo11()
    {
        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            DELETE FROM SchemaVersion WHERE Version = 12;

            CREATE TABLE IF NOT EXISTS TeamRosterProjection (
                DraftId TEXT NOT NULL,
                BranchId TEXT NOT NULL,
                TeamId TEXT NOT NULL,
                PlayerId TEXT NOT NULL,
                OverallPick INTEGER NOT NULL,
                PRIMARY KEY (DraftId, BranchId, TeamId, PlayerId)
            );

            CREATE TABLE IF NOT EXISTS PlayerAvailabilityProjection (
                DraftId TEXT NOT NULL,
                BranchId TEXT NOT NULL,
                PlayerId TEXT NOT NULL,
                IsAvailable INTEGER NOT NULL,
                PRIMARY KEY (DraftId, BranchId, PlayerId)
            );
            """;
        cmd.ExecuteNonQuery();
    }
}
