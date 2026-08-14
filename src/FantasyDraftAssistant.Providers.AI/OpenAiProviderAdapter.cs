using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Core.Serialization;

namespace FantasyDraftAssistant.Providers.AI;

public sealed class OpenAiProviderAdapter(
    ICredentialStore credentials,
    IDraftQueryService queries,
    IAiUsageService usage) : IAiProviderAdapter
{
    public const string DefaultModel = "gpt-4.1";
    public string ProviderKey => AiProviderCatalog.OpenAi;

    public async Task<AiConnectionTestResult> TestConnectionAsync(AiProviderConfig config, CancellationToken cancellationToken)
    {
        var key = await credentials.GetSecretAsync("ai", ProviderKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return new AiConnectionTestResult { Succeeded = false, Message = "OpenAI API key is missing." };

        try
        {
            using var client = CreateClient(key);
            using var response = await client.GetAsync("https://api.openai.com/v1/models", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AiConnectionTestResult
                {
                    Succeeded = false,
                    Message = $"OpenAI authentication failed ({(int)response.StatusCode}): {Trim(body)}"
                };
            }

            return new AiConnectionTestResult
            {
                Succeeded = true,
                Message = "Connected to OpenAI.",
                Model = string.IsNullOrWhiteSpace(config.Model) ? DefaultModel : config.Model
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
        var key = await credentials.GetSecretAsync("ai", ProviderKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            yield return new AiResponseChunk { IsComplete = true, Error = "OpenAI API key is missing.", AnalyzedStateVersion = request.StateVersion };
            yield break;
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model;
        var context = await ResolveContextAsync(request, queries, cancellationToken);
        var payload = new
        {
            model,
            stream = true,
            messages = new[]
            {
                new { role = "system", content = "You are a fantasy football draft analyst. Use only the provided draft context." },
                new { role = "user", content = DraftAnalystPrompt.Build(request, context) }
            }
        };

        using var client = CreateClient(key);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        await foreach (var chunk in StreamSseAsync(client, httpRequest, request, model, started, ExtractOpenAiDelta, usage, cancellationToken))
            yield return chunk;
    }

    internal static async Task<string> ResolveContextAsync(AiAnalysisRequest request, IDraftQueryService queries, CancellationToken cancellationToken)
    {
        if (request.DecisionContextJson is not null)
            return request.DecisionContextJson;
        var dto = await queries.GetDecisionContextAsync(new QueryContext
        {
            DraftId = request.DraftId,
            BranchId = request.BranchId
        }, cancellationToken);
        return DraftJson.Serialize(dto);
    }

    internal static async IAsyncEnumerable<AiResponseChunk> StreamSseAsync(
        HttpClient client,
        HttpRequestMessage httpRequest,
        AiAnalysisRequest request,
        string model,
        DateTimeOffset started,
        Func<string, string?> extract,
        IAiUsageService usage,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
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
                    Error = $"Request failed ({(int)response.StatusCode}): {Trim(body)}",
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
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.Ordinal))
                    continue;
                var data = line["data:".Length..].Trim();
                if (data is "[DONE]")
                    break;
                var text = extract(data);
                if (string.IsNullOrEmpty(text))
                    continue;
                builder.Append(text);
                yield return new AiResponseChunk { Text = text, AnalyzedStateVersion = request.StateVersion };
            }

            var latency = DateTimeOffset.UtcNow - started;
            await usage.RecordAsync(new AiUsageRecord
            {
                DraftId = request.DraftId,
                Provider = AiProviderCatalog.OpenAi,
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
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    private static string? ExtractOpenAiDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;
            var delta = choices[0].GetProperty("delta");
            return delta.TryGetProperty("content", out var content) ? content.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Trim(string value) => value.Length <= 240 ? value : value[..240];
}
