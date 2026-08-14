using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Core.Serialization;

namespace FantasyDraftAssistant.Providers.AI;

public sealed class XaiProviderAdapter(
    ICredentialStore credentials,
    IDraftQueryService queries,
    IAiUsageService usage) : IAiProviderAdapter
{
    public const string Key = "xai";
    public const string DefaultModel = "grok-4.6";
    public const string BaseUrl = "https://api.x.ai/v1/";

    public string ProviderKey => Key;

    public async Task<AiConnectionTestResult> TestConnectionAsync(AiProviderConfig config, CancellationToken cancellationToken)
    {
        var key = await credentials.GetSecretAsync("ai", Key, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return new AiConnectionTestResult { Succeeded = false, Message = "API key is missing." };

        try
        {
            using var client = CreateClient(key);
            using var response = await client.GetAsync("models", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new AiConnectionTestResult { Succeeded = false, Message = $"Authentication or network failure ({(int)response.StatusCode})." };

            var model = string.IsNullOrWhiteSpace(config.Model) ? DefaultModel : config.Model;
            return new AiConnectionTestResult
            {
                Succeeded = true,
                Message = "Connected to xAI.",
                Model = model
            };
        }
        catch (Exception ex)
        {
            return new AiConnectionTestResult { Succeeded = false, Message = ex.Message };
        }
    }

    public async IAsyncEnumerable<AiResponseChunk> StreamAnalysisAsync(
        AiAnalysisRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var started = DateTimeOffset.UtcNow;
        var key = await credentials.GetSecretAsync("ai", Key, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            yield return new AiResponseChunk { IsComplete = true, Error = "xAI API key is missing.", AnalyzedStateVersion = request.StateVersion };
            yield break;
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model;
        var context = request.DecisionContextJson;
        if (context is null)
        {
            var dto = await queries.GetDecisionContextAsync(new QueryContext
            {
                DraftId = request.DraftId,
                BranchId = request.BranchId
            }, cancellationToken);
            context = DraftJson.Serialize(dto);
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["stream"] = true,
            ["input"] = DraftAnalystPrompt.Build(request, context)
        };

        using var client = CreateClient(key);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "responses")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        HttpResponseMessage? response = null;
        string? error = null;
        try
        {
            response = await client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }

        if (error is not null)
        {
            yield return new AiResponseChunk { IsComplete = true, Error = error, AnalyzedStateVersion = request.StateVersion };
            yield break;
        }

        using (response)
        {
            if (!response!.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return new AiResponseChunk
                {
                    IsComplete = true,
                    Error = $"xAI request failed ({(int)response.StatusCode}): {Trim(body)}",
                    AnalyzedStateVersion = request.StateVersion
                };
                yield break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var builder = new StringBuilder();
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (!line.StartsWith("data:", StringComparison.Ordinal))
                    continue;
                var data = line["data:".Length..].Trim();
                if (data is "[DONE]")
                    break;

                var text = ExtractDelta(data);
                if (string.IsNullOrEmpty(text))
                    continue;
                builder.Append(text);
                yield return new AiResponseChunk
                {
                    Text = text,
                    AnalyzedStateVersion = request.StateVersion
                };
            }

            var latency = DateTimeOffset.UtcNow - started;
            await usage.RecordAsync(new AiUsageRecord
            {
                DraftId = request.DraftId,
                Provider = Key,
                Model = model,
                AnalyzedStateVersion = request.StateVersion,
                Latency = latency,
                RequestStartedAt = started,
                ResponseCompletedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            yield return new AiResponseChunk
            {
                Text = builder.Length == 0 ? "No response text was returned." : null,
                IsComplete = true,
                AnalyzedStateVersion = request.StateVersion,
                Latency = latency
            };
        }
    }

    private static HttpClient CreateClient(string apiKey)
    {
        var client = new HttpClient { BaseAddress = new Uri(BaseUrl), Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    private static string? ExtractDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.String)
                return delta.GetString();
            if (root.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                return text.GetString();
            if (root.TryGetProperty("output_text", out var output) && output.ValueKind == JsonValueKind.String)
                return output.GetString();
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Trim(string value) => value.Length <= 240 ? value : value[..240];
}
