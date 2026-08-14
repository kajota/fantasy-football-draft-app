using System.Reflection;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Database;

public sealed class MigrationRunner(SqliteConnectionFactory factory)
{
    public void Apply()
    {
        using var connection = factory.Open();
        EnsureVersionTable(connection);
        var applied = GetApplied(connection);
        foreach (var (version, sql) in LoadMigrations())
        {
            if (applied.Contains(version))
                continue;

            using var tx = connection.BeginTransaction();
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO SchemaVersion(Version, AppliedAt) VALUES ($v, $t);";
                cmd.Parameters.AddWithValue("$v", version);
                cmd.Parameters.AddWithValue("$t", DateTimeOffset.UtcNow.ToString("O"));
                cmd.ExecuteNonQuery();
            }

            tx.Commit();
        }
    }

    private static void EnsureVersionTable(SqliteConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS SchemaVersion (
                Version INTEGER PRIMARY KEY,
                AppliedAt TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    private static HashSet<int> GetApplied(SqliteConnection connection)
    {
        var set = new HashSet<int>();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Version FROM SchemaVersion;";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            set.Add(reader.GetInt32(0));
        return set;
    }

    private static IEnumerable<(int Version, string Sql)> LoadMigrations()
    {
        var assembly = typeof(MigrationRunner).Assembly;
        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase)).OrderBy(n => n))
        {
            var file = name.Split('.').Reverse().Skip(1).First();
            var digits = new string(file.TakeWhile(char.IsDigit).ToArray());
            if (!int.TryParse(digits, out var version))
                continue;
            using var stream = assembly.GetManifestResourceStream(name)
                               ?? throw new InvalidOperationException($"Missing migration {name}.");
            using var reader = new StreamReader(stream);
            yield return (version, reader.ReadToEnd());
        }
    }
}
