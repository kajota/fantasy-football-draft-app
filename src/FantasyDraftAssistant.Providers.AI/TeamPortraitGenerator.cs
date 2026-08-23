using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Providers.AI;

public sealed class TeamPortraitGenerator(
    ICredentialStore credentials,
    ITeamPortraitStore store,
    HttpClient http) : ITeamPortraitGenerator
{
    public async Task<TeamPortraitResult> GenerateAsync(TeamPortraitRequest request, CancellationToken cancellationToken = default)
    {
        var prompt = TeamPortraitPrompt.Build(
            request.TeamName,
            request.OwnerName,
            request.Tone,
            request.TeamId,
            request.PortraitNotes,
            request.Spin,
            request.ArtStyleKey);
        var provider = string.IsNullOrWhiteSpace(request.ProviderKey)
            ? AiProviderCatalog.Xai
            : request.ProviderKey.Trim();

        if (provider.Equals(AiProviderCatalog.Xai, StringComparison.OrdinalIgnoreCase))
        {
            var key = await credentials.GetSecretAsync("ai", AiProviderCatalog.Xai, cancellationToken);
            if (string.IsNullOrWhiteSpace(key))
                return MissingKey("Grok", "console.x.ai");
            return await GenerateXaiAsync(key, prompt, request, cancellationToken);
        }

        if (provider.Equals(AiProviderCatalog.OpenAi, StringComparison.OrdinalIgnoreCase))
        {
            var key = await credentials.GetSecretAsync("ai", AiProviderCatalog.OpenAi, cancellationToken);
            if (string.IsNullOrWhiteSpace(key))
                return MissingKey("ChatGPT", "platform.openai.com");
            return await GenerateOpenAiAsync(key, prompt, request, cancellationToken);
        }

        return new TeamPortraitResult
        {
            Succeeded = false,
            Error = "Pick Grok or ChatGPT for team images. Claude does not generate pictures."
        };
    }

    private static TeamPortraitResult MissingKey(string product, string where) =>
        new()
        {
            Succeeded = false,
            Error = $"Add a {product} API key under AI Providers ({where}). Images are billed to that API account."
        };

    private async Task<TeamPortraitResult> GenerateXaiAsync(
        string apiKey,
        string prompt,
        TeamPortraitRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = "grok-imagine-image-2.0",
            ["prompt"] = prompt,
            ["aspect_ratio"] = "1:1",
            ["resolution"] = "1k",
            ["quality"] = "medium",
            ["response_format"] = "b64_json"
        };
        return await SendAsync(
            "https://api.x.ai/v1/images/generations",
            apiKey,
            payload,
            request,
            AiProviderCatalog.Xai,
            "grok-imagine-image-2.0",
            cancellationToken);
    }

    private async Task<TeamPortraitResult> GenerateOpenAiAsync(
        string apiKey,
        string prompt,
        TeamPortraitRequest request,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["model"] = "gpt-image-1",
            ["prompt"] = prompt,
            ["size"] = "1024x1024"
        };
        return await SendAsync(
            "https://api.openai.com/v1/images/generations",
            apiKey,
            payload,
            request,
            AiProviderCatalog.OpenAi,
            "gpt-image-1",
            cancellationToken);
    }

    private async Task<TeamPortraitResult> SendAsync(
        string url,
        string apiKey,
        object payload,
        TeamPortraitRequest request,
        string provider,
        string model,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            using var response = await http.SendAsync(httpRequest, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new TeamPortraitResult
                {
                    Succeeded = false,
                    Error = ReadError(body) ?? $"Image request failed ({(int)response.StatusCode}).",
                    Provider = provider,
                    Model = model
                };
            }

            var bytes = await ReadImageAsync(body, http, cancellationToken);
            if (bytes is null || bytes.Length == 0)
            {
                return new TeamPortraitResult
                {
                    Succeeded = false,
                    Error = "The image API returned no picture. Try again.",
                    Provider = provider,
                    Model = model
                };
            }

            await store.SaveAsync(request.TeamId, bytes, cancellationToken);
            return new TeamPortraitResult { Succeeded = true, Provider = provider, Model = model };
        }
        catch (Exception ex)
        {
            return new TeamPortraitResult { Succeeded = false, Error = ex.Message, Provider = provider, Model = model };
        }
    }

    private static async Task<byte[]?> ReadImageAsync(string body, HttpClient client, CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("data", out var data) || data.GetArrayLength() == 0)
            return null;
        var first = data[0];
        if (first.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String)
        {
            var text = b64.GetString();
            return string.IsNullOrWhiteSpace(text) ? null : Convert.FromBase64String(text);
        }

        if (first.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
        {
            var href = url.GetString();
            if (string.IsNullOrWhiteSpace(href))
                return null;
            return await client.GetByteArrayAsync(href, cancellationToken);
        }

        return null;
    }

    private static string? ReadError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return error.GetString();
                if (error.TryGetProperty("message", out var message))
                    return message.GetString();
            }
        }
        catch (JsonException)
        {
        }

        return body.Length <= 240 ? body : body[..240];
    }
}
