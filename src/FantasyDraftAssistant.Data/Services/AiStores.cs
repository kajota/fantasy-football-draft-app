using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class AiConfigStore(SqliteConnectionFactory factory) : IAiConfigStore
{
    public Task<IReadOnlyList<AiProviderConfig>> ListAsync(CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("SELECT ProviderKey, Enabled, Model, Role, PerDraftSpendLimit, PerSessionSpendLimit FROM AiProviderConfigs;");
        using var reader = cmd.ExecuteReader();
        var list = new List<AiProviderConfig>();
        while (reader.Read())
        {
            list.Add(new AiProviderConfig
            {
                ProviderKey = reader.GetString(0),
                Enabled = reader.GetInt32(1) == 1,
                Model = reader.GetString(2),
                Role = reader.GetString(3),
                PerDraftSpendLimit = reader.IsDBNull(4) ? null : decimal.Parse(reader.GetString(4)),
                PerSessionSpendLimit = reader.IsDBNull(5) ? null : decimal.Parse(reader.GetString(5))
            });
        }

        foreach (var descriptor in AiProviderCatalog.All)
        {
            if (list.Any(c => c.ProviderKey == descriptor.ProviderKey))
                continue;
            list.Add(new AiProviderConfig
            {
                ProviderKey = descriptor.ProviderKey,
                Enabled = false,
                Model = descriptor.DefaultModel,
                Role = "Fast Advisor"
            });
        }

        return Task.FromResult<IReadOnlyList<AiProviderConfig>>(list);
    }

    public Task SaveAsync(AiProviderConfig config, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            INSERT INTO AiProviderConfigs(ProviderKey, Enabled, Model, Role, PerDraftSpendLimit, PerSessionSpendLimit)
            VALUES ($k, $e, $m, $r, $d, $s)
            ON CONFLICT(ProviderKey) DO UPDATE SET
                Enabled = excluded.Enabled,
                Model = excluded.Model,
                Role = excluded.Role,
                PerDraftSpendLimit = excluded.PerDraftSpendLimit,
                PerSessionSpendLimit = excluded.PerSessionSpendLimit;
            """)
            .Bind("$k", config.ProviderKey)
            .Bind("$e", config.Enabled ? 1 : 0)
            .Bind("$m", config.Model)
            .Bind("$r", config.Role)
            .Bind("$d", config.PerDraftSpendLimit?.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Bind("$s", config.PerSessionSpendLimit?.ToString(System.Globalization.CultureInfo.InvariantCulture));
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }
}

public sealed class AiUsageService(SqliteConnectionFactory factory) : IAiUsageService
{
    public Task RecordAsync(AiUsageRecord record, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            INSERT INTO AiUsageRecords(UsageId, DraftId, Provider, Model, AnalyzedStateVersion, InputTokens, OutputTokens, EstimatedCost, LatencyMs, RequestStartedAt, ResponseCompletedAt)
            VALUES ($id, $draft, $p, $m, $v, $in, $out, $cost, $lat, $start, $end);
            """)
            .Bind("$id", Guid.NewGuid().ToString("D"))
            .Bind("$draft", record.DraftId.ToString())
            .Bind("$p", record.Provider)
            .Bind("$m", record.Model)
            .Bind("$v", record.AnalyzedStateVersion)
            .Bind("$in", record.InputTokens)
            .Bind("$out", record.OutputTokens)
            .Bind("$cost", record.EstimatedCost?.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .Bind("$lat", record.Latency.HasValue ? (int)record.Latency.Value.TotalMilliseconds : null)
            .Bind("$start", record.RequestStartedAt.ToString("O"))
            .Bind("$end", record.ResponseCompletedAt?.ToString("O"));
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AiUsageRecord>> ListForDraftAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("SELECT * FROM AiUsageRecords WHERE DraftId = $d ORDER BY RequestStartedAt;", null)
            .Bind("$d", draftId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<AiUsageRecord>();
        while (reader.Read())
        {
            list.Add(new AiUsageRecord
            {
                DraftId = draftId,
                Provider = reader.GetString(reader.GetOrdinal("Provider")),
                Model = reader.GetString(reader.GetOrdinal("Model")),
                AnalyzedStateVersion = reader.GetInt32(reader.GetOrdinal("AnalyzedStateVersion")),
                InputTokens = reader.GetNullInt(reader.GetOrdinal("InputTokens")),
                OutputTokens = reader.GetNullInt(reader.GetOrdinal("OutputTokens")),
                EstimatedCost = reader.IsDBNull(reader.GetOrdinal("EstimatedCost")) ? null : decimal.Parse(reader.GetString(reader.GetOrdinal("EstimatedCost"))),
                Latency = reader.IsDBNull(reader.GetOrdinal("LatencyMs")) ? null : TimeSpan.FromMilliseconds(reader.GetInt32(reader.GetOrdinal("LatencyMs"))),
                RequestStartedAt = reader.GetTime(reader.GetOrdinal("RequestStartedAt")),
                ResponseCompletedAt = reader.GetNullTime(reader.GetOrdinal("ResponseCompletedAt"))
            });
        }

        return Task.FromResult<IReadOnlyList<AiUsageRecord>>(list);
    }

    public async Task<decimal> EstimatedDraftSpendAsync(DraftId draftId, string? providerKey = null, CancellationToken cancellationToken = default)
    {
        var records = await ListForDraftAsync(draftId, cancellationToken);
        return records
            .Where(r => providerKey is null || r.Provider == providerKey)
            .Sum(r => r.EstimatedCost ?? 0);
    }
}
