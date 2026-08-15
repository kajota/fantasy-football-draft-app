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
            var model = string.IsNullOrWhiteSpace(config.Model) ? DefaultModel : config.Model;
            using var client = CreateClient(key);
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
            {
                Content = JsonContent(ChatCompletionBody(
                    model,
                    maxOutputTokens: RequiresMaxCompletionTokens(model) ? 32 : 1,
                    messages: new[] { new { role = "user", content = "ping" } },
                    fastMode: true))
            };
            using var response = await client.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new AiConnectionTestResult
                {
                    Succeeded = false,
                    Message = FormatOpenAiError((int)response.StatusCode, body)
                };
            }

            return new AiConnectionTestResult
            {
                Succeeded = true,
                Message = "Connected to OpenAI. Completions are billed to the API account, not ChatGPT Plus.",
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
        var key = await credentials.GetSecretAsync("ai", ProviderKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(key))
        {
            yield return new AiResponseChunk { IsComplete = true, Error = "OpenAI API key is missing.", AnalyzedStateVersion = request.StateVersion };
            yield break;
        }

        var model = string.IsNullOrWhiteSpace(request.Model) ? DefaultModel : request.Model;
        var context = await ResolveContextAsync(request, queries, cancellationToken);
        using var client = CreateClient(key, AiOutputBudget.HttpTimeoutSeconds(model, request.FastMode));
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://api.openai.com/v1/chat/completions")
        {
            Content = JsonContent(ChatCompletionBody(
                model,
                maxOutputTokens: AiOutputBudget.MaxOutputTokens(model, request.FastMode),
                messages: new[]
                {
                    new { role = "system", content = "You are a fantasy football draft analyst. Answer the user's question using only the provided draft context. Do not force a 'who should I pick' recommendation unless they asked that." },
                    new { role = "user", content = DraftAnalystPrompt.Build(request, context) }
                },
                stream: true,
                fastMode: request.FastMode))
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
                    Error = FormatOpenAiError((int)response.StatusCode, body),
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
                stopReason ??= ExtractOpenAiFinishReason(data);
                var text = extract(data);
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
                Provider = AiProviderCatalog.OpenAi,
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

    public static bool RequiresMaxCompletionTokens(string? model) =>
        AiOutputBudget.UsesHiddenReasoning(model);

    public static Dictionary<string, object?> ChatCompletionBody(
        string model,
        int maxOutputTokens,
        object messages,
        bool stream = false,
        bool fastMode = true)
    {
        var body = new Dictionary<string, object?>
        {
            ["model"] = model,
            ["messages"] = messages
        };
        if (stream)
            body["stream"] = true;
        if (RequiresMaxCompletionTokens(model))
        {
            body["max_completion_tokens"] = maxOutputTokens;
            body["reasoning_effort"] = AiOutputBudget.Effort(fastMode);
        }
        else
        {
            body["max_tokens"] = maxOutputTokens;
        }

        return body;
    }

    private static StringContent JsonContent(object value) =>
        new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

    private static HttpClient CreateClient(string apiKey, int timeoutSeconds = 60)
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return client;
    }

    public static string? ExtractOpenAiDelta(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;
            var choice = choices[0];
            if (choice.TryGetProperty("delta", out var delta))
            {
                var text = ReadContent(delta);
                if (!string.IsNullOrEmpty(text))
                    return text;
            }

            return choice.TryGetProperty("message", out var message) ? ReadContent(message) : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? ExtractOpenAiFinishReason(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
                return null;
            return choices[0].TryGetProperty("finish_reason", out var reason) && reason.ValueKind == JsonValueKind.String
                ? reason.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadContent(JsonElement element)
    {
        if (!element.TryGetProperty("content", out var content))
            return null;
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString();
        if (content.ValueKind != JsonValueKind.Array)
            return null;

        var builder = new StringBuilder();
        foreach (var part in content.EnumerateArray())
        {
            if (part.ValueKind == JsonValueKind.String)
                builder.Append(part.GetString());
            else if (part.TryGetProperty("text", out var text))
                builder.Append(text.GetString());
        }

        return builder.Length == 0 ? null : builder.ToString();
    }

    public static string FormatOpenAiError(int status, string body)
    {
        var code = ReadError(body, "code");
        var type = ReadError(body, "type");
        var message = ReadError(body, "message");
        if (code is "billing_not_active" || type is "billing_not_active"
            || (message is not null && message.Contains("billing", StringComparison.OrdinalIgnoreCase)))
        {
            return "OpenAI says this API account is not billed yet. A ChatGPT Plus subscription does not pay for the API. Add a payment method and prepaid credit at platform.openai.com/settings/organization/billing, then wait a minute and Ask again.";
        }

        if (status == 429)
            return "OpenAI rate-limited or refused the request. Check usage limits and billing at platform.openai.com.";

        return message is { Length: > 0 }
            ? $"OpenAI request failed ({status}): {message}"
            : $"OpenAI request failed ({status}): {Trim(body)}";
    }

    private static string? ReadError(string body, string name)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var error))
                return null;
            return error.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Trim(string value) => value.Length <= 240 ? value : value[..240];
}
