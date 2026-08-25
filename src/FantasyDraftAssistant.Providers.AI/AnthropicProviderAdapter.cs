using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.AI;

public sealed class AnthropicProviderAdapter(
    ICredentialStore credentials,
    IDraftQueryService queries,
    IAiUsageService usage) : IAiProviderAdapter
{
    public static string DefaultModel =>
        AiProviderCatalog.Find(AiProviderCatalog.Anthropic)?.DefaultModel ?? "claude-sonnet-5";
    public string ProviderKey => AiProviderCatalog.Anthropic;

    public async Task<AiConnectionTestResult> TestConnectionAsync(AiProviderConfig config, CancellationToken cancellationToken)
    {
        var key = await credentials.GetSecretAsync("ai", ProviderKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
            return new AiConnectionTestResult { Succeeded = false, Message = "Anthropic API key is missing." };

        try
        {
            using var client = CreateClient(key);
            using var response = await client.GetAsync("https://api.anthropic.com/v1/models", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return new AiConnectionTestResult
                {
                    Succeeded = false,
                    Message = $"Anthropic authentication failed ({(int)response.StatusCode}): {Trim(body)}"
                };
            }

            return new AiConnectionTestResult
            {
                Succeeded = true,
                Message = "Connected to Anthropic.",
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
            yield return new AiResponseChunk { IsComplete = true, Error = "Anthropic API key is missing.", AnalyzedStateVersion = request.StateVersion };
            yield break;
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model;
        var context = await OpenAiProviderAdapter.ResolveContextAsync(request, queries, cancellationToken);
        using var client = CreateClient(key, AiOutputBudget.HttpTimeoutSeconds(model, request.FastMode));
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.anthropic.com/v1/messages")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(MessagesBody(model, request.FastMode, DraftAnalystPrompt.Build(request, context))),
                Encoding.UTF8,
                "application/json")
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
                    Error = $"Anthropic request failed ({(int)response.StatusCode}): {Trim(body)}",
                    AnalyzedStateVersion = request.StateVersion
                };
                yield break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            var builder = new StringBuilder();
            string? stopReason = null;
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
                stopReason ??= ExtractAnthropicStopReason(data);
                var text = ExtractAnthropicDelta(data);
                if (string.IsNullOrEmpty(text))
                    continue;
                builder.Append(text);
                yield return new AiResponseChunk { Text = text, AnalyzedStateVersion = request.StateVersion };
            }

            var note = AiOutputBudget.EmptyOrTruncatedNote(model, stopReason, builder.Length > 0);
            if (note.Length > 0)
            {
                builder.Append(note);
                yield return new AiResponseChunk { Text = note, AnalyzedStateVersion = request.StateVersion };
            }

            var latency = DateTimeOffset.UtcNow - started;
            await usage.RecordAsync(new AiUsageRecord
            {
                DraftId = request.DraftId,
                Provider = ProviderKey,
                Model = model,
                AnalyzedStateVersion = request.StateVersion,
                EstimatedCost = AiCostEstimate.EstimateUsd(
                    model,
                    (request.DecisionContextJson?.Length ?? 0) + request.Prompt.Length,
                    builder.Length),
                Latency = latency,
                RequestStartedAt = started,
                ResponseCompletedAt = DateTimeOffset.UtcNow
            }, cancellationToken);

            yield return new AiResponseChunk
            {
                IsComplete = true,
                AnalyzedStateVersion = request.StateVersion,
                Latency = latency
            };
        }
    }

    public static Dictionary<string, object?> MessagesBody(string model, bool fastMode, string userContent)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["max_tokens"] = AiOutputBudget.MaxOutputTokens(model, fastMode),
            ["stream"] = true,
            ["messages"] = new[] { new { role = "user", content = userContent } }
        };
        if (AiOutputBudget.SupportsClaudeEffort(model))
            body["output_config"] = new Dictionary<string, object?> { ["effort"] = AiOutputBudget.Effort(fastMode) };
        return body;
    }

    private static HttpClient CreateClient(string apiKey, int timeoutSeconds = 60)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("x-api-key", apiKey);
        client.DefaultRequestHeaders.TryAddWithoutValidation("anthropic-version", "2023-06-01");
        return client;
    }

    public static string? ExtractAnthropicDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var type) || type.GetString() != "content_block_delta")
                return null;
            if (!root.TryGetProperty("delta", out var delta))
                return null;
            return delta.TryGetProperty("text", out var text) ? text.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ExtractAnthropicStopReason(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.TryGetProperty("delta", out var delta)
                && delta.TryGetProperty("stop_reason", out var fromDelta)
                && fromDelta.ValueKind == JsonValueKind.String)
            {
                return fromDelta.GetString();
            }

            return root.TryGetProperty("stop_reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Trim(string value) => value.Length <= 240 ? value : value[..240];
}
