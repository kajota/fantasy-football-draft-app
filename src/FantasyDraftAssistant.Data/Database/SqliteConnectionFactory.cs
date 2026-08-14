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
}
