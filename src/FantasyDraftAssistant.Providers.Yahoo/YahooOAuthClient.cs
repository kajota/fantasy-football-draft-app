using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FantasyDraftAssistant.Providers.Yahoo;

public sealed class YahooOAuthClient(HttpClient http)
{
    public const string AuthorizeUrl = "https://api.login.yahoo.com/oauth2/request_auth";
    public const string TokenUrl = "https://api.login.yahoo.com/oauth2/get_token";

    public string BuildAuthorizationUrl(string clientId, string redirectUri, string state)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["response_type"] = "code",
            ["language"] = "en-us",
            ["state"] = state
        };
        return QueryHelpers.Add(AuthorizeUrl, query);
    }

    public Task<YahooTokenResponse> ExchangeCodeAsync(
        string clientId,
        string clientSecret,
        string redirectUri,
        string code,
        CancellationToken cancellationToken) =>
        RequestTokenAsync(clientId, clientSecret, new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri
        }, cancellationToken);

    public Task<YahooTokenResponse> RefreshAsync(
        string clientId,
        string clientSecret,
        string refreshToken,
        CancellationToken cancellationToken) =>
        RequestTokenAsync(clientId, clientSecret, new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        }, cancellationToken);

    private async Task<YahooTokenResponse> RequestTokenAsync(
        string clientId,
        string clientSecret,
        Dictionary<string, string> form,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}")));
        request.Content = new FormUrlEncodedContent(form);

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(DescribeTokenError(response.StatusCode, body));
        }

        var parsed = JsonSerializer.Deserialize<YahooTokenResponse>(body, Json)
                     ?? throw new InvalidOperationException("Yahoo token response was empty.");
        if (string.IsNullOrWhiteSpace(parsed.AccessToken))
            throw new InvalidOperationException("Yahoo did not return an access token.");
        return parsed;
    }

    private static string DescribeTokenError(System.Net.HttpStatusCode status, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var error = doc.RootElement.TryGetProperty("error_description", out var desc)
                ? desc.GetString()
                : doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : null;
            if (!string.IsNullOrWhiteSpace(error))
                return $"Yahoo sign-in failed ({(int)status}): {error}";
        }
        catch (JsonException)
        {
        }

        return $"Yahoo sign-in failed ({(int)status}). Check the Client ID, secret, and redirect URI on the Yahoo developer app.";
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

public sealed class YahooTokenResponse
{
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public int ExpiresIn { get; init; }
    public string? TokenType { get; init; }
}

internal static class QueryHelpers
{
    public static string Add(string url, IReadOnlyDictionary<string, string> query)
    {
        var builder = new StringBuilder(url);
        var first = !url.Contains('?');
        foreach (var (key, value) in query)
        {
            builder.Append(first ? '?' : '&');
            first = false;
            builder.Append(Uri.EscapeDataString(key));
            builder.Append('=');
            builder.Append(Uri.EscapeDataString(value));
        }

        return builder.ToString();
    }
}
