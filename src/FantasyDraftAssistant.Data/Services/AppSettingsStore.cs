using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class AppSettingsStore(SqliteConnectionFactory factory) : IAppSettingsStore
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = factory.Open();
        using var cmd = db.Cmd("SELECT Value FROM AppSettings WHERE Key = $k;").Bind("$k", key);
        return Task.FromResult(cmd.ExecuteScalar() as string);
    }

    public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            INSERT INTO AppSettings(Key, Value) VALUES ($k, $v)
            ON CONFLICT(Key) DO UPDATE SET Value = $v;
            """)
            .Bind("$k", key)
            .Bind("$v", value);
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}
