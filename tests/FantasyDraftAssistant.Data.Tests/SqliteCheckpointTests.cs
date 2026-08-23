using FantasyDraftAssistant.Data.Database;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Tests;

public class SqliteCheckpointTests
{
    [Fact]
    public void Checkpoint_truncates_wal_into_the_main_database()
    {
        var root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        try
        {
            var factory = new SqliteConnectionFactory(new AppPaths(root));
            using (var db = factory.Open())
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = "CREATE TABLE t(x INTEGER); INSERT INTO t VALUES (1);";
                cmd.ExecuteNonQuery();
            }

            factory.Checkpoint();

            var wal = factory.Paths.DatabasePath + "-wal";
            Assert.True(!File.Exists(wal) || new FileInfo(wal).Length == 0);

            using var verify = factory.Open();
            using var read = verify.CreateCommand();
            read.CommandText = "SELECT x FROM t";
            Assert.Equal(1L, (long)read.ExecuteScalar()!);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
