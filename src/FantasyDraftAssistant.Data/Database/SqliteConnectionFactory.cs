using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Database;

public sealed class SqliteConnectionFactory(AppPaths paths)
{
    public AppPaths Paths { get; } = paths;

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection($"Data Source={Paths.DatabasePath}");
        connection.Open();
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                PRAGMA foreign_keys = ON;
                PRAGMA journal_mode = WAL;
                PRAGMA synchronous = FULL;
                PRAGMA busy_timeout = 5000;
                """;
            cmd.ExecuteNonQuery();
        }

        return connection;
    }

    public SqliteConnection Open(string path)
    {
        var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Flushes WAL into <c>draft.db</c> and truncates the sidecar files so a
    /// folder sync (Synology Drive, etc.) copies one complete database.
    /// </summary>
    public void Checkpoint()
    {
        SqliteConnection.ClearAllPools();
        if (!File.Exists(Paths.DatabasePath))
            return;

        using var connection = Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
        cmd.ExecuteNonQuery();
        connection.Close();
        SqliteConnection.ClearAllPools();
    }
}
