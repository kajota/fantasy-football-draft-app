using System.Reflection;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Database;

/// <summary>
/// A migration that cannot be expressed in SQL — typically because it has to compute
/// something the database does not know how to compute, such as a hashed identifier.
/// </summary>
public interface ICodeMigration
{
    int Version { get; }
    string Name { get; }

    /// <summary>Copy the database aside before running. Use for anything that rewrites keys.</summary>
    bool RequiresBackup { get; }

    void Apply(SqliteConnection connection, SqliteTransaction transaction);
}

public sealed class MigrationRunner(SqliteConnectionFactory factory)
{
    private static readonly ICodeMigration[] CodeMigrations = [new PlayerIdentityMigration()];

    public void Apply()
    {
        using var connection = factory.Open();
        EnsureVersionTable(connection);
        var applied = GetApplied(connection);

        var steps = LoadMigrations()
            .Select(m => (m.Version, Sql: (string?)m.Sql, Code: (ICodeMigration?)null))
            .Concat(CodeMigrations.Select(c => (c.Version, Sql: (string?)null, Code: (ICodeMigration?)c)))
            .OrderBy(step => step.Version)
            .ToList();

        foreach (var (version, sql, code) in steps)
        {
            if (applied.Contains(version))
                continue;

            if (code is { RequiresBackup: true })
                BackupBefore(code);

            using var tx = connection.BeginTransaction();
            if (sql is not null)
            {
                using var cmd = connection.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }

            code?.Apply(connection, tx);

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

    /// <summary>
    /// Snapshots the database next to the normal backups. Best effort: a migration that cannot
    /// be backed up (no file yet, in-memory test database) still runs, inside its transaction.
    /// </summary>
    private void BackupBefore(ICodeMigration migration)
    {
        try
        {
            if (!File.Exists(factory.Paths.DatabasePath))
                return;

            var stamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
            var dest = Path.Combine(factory.Paths.BackupDirectory, $"draft-{stamp}-before-{migration.Name}.db");
            using var source = factory.Open();
            using var backup = factory.Open(dest);
            source.BackupDatabase(backup);
        }
        catch (Exception ex) when (ex is IOException or SqliteException)
        {
            // Never block a migration on the backup; the transaction still protects the data.
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
