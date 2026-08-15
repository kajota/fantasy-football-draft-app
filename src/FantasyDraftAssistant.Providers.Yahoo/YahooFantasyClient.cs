using System.Net.Http.Headers;

namespace FantasyDraftAssistant.Providers.Yahoo;

public sealed class YahooFantasyClient(HttpClient http)
{
    public const string BaseUrl = "https://fantasysports.yahooapis.com/fantasy/v2/";

    public Task<string> GetUserLeaguesXmlAsync(string accessToken, CancellationToken cancellationToken) =>
        GetXmlAsync(accessToken, "users;use_login=1/games;game_codes=nfl/leagues", cancellationToken);

    public Task<string> GetLeagueSettingsXmlAsync(string accessToken, string leagueKey, CancellationToken cancellationToken) =>
        GetXmlAsync(accessToken, $"league/{Uri.EscapeDataString(leagueKey)}/settings", cancellationToken);

    public Task<string> GetLeagueTeamsXmlAsync(string accessToken, string leagueKey, CancellationToken cancellationToken) =>
        GetXmlAsync(accessToken, $"league/{Uri.EscapeDataString(leagueKey)}/teams", cancellationToken);

    private async Task<string> GetXmlAsync(string accessToken, string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.ParseAdd("application/xml");

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Yahoo Fantasy API failed ({(int)response.StatusCode}) for {path}. {ShortError(body)}");
        }

        return body;
    }

    private static string ShortError(string body)
    {
        var trimmed = body.ReplaceLineEndings(" ").Trim();
        return trimmed.Length <= 240 ? trimmed : trimmed[..240] + "…";
    }
}
