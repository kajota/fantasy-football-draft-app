using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Providers.Yahoo;

public sealed class YahooAuthService(ICredentialStore credentials, YahooOAuthClient oauth) : IYahooAuthService
{
    public const string Scope = "yahoo";
    public const string ClientIdKey = "client_id";
    public const string ClientSecretKey = "client_secret";
    public const string RedirectUriKey = "redirect_uri";
    public const string AccessTokenKey = "access_token";
    public const string RefreshTokenKey = "refresh_token";
    public const string ExpiresAtKey = "access_token_expires";
    public const string PendingStateKey = "pending_state";

    public async Task<YahooAuthStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var clientId = await credentials.GetSecretAsync(Scope, ClientIdKey, cancellationToken);
        var access = await credentials.GetSecretAsync(Scope, AccessTokenKey, cancellationToken);
        var refresh = await credentials.GetSecretAsync(Scope, RefreshTokenKey, cancellationToken);
        var expiresRaw = await credentials.GetSecretAsync(Scope, ExpiresAtKey, cancellationToken);
        DateTimeOffset? expires = DateTimeOffset.TryParse(expiresRaw, out var parsed) ? parsed : null;
        return new YahooAuthStatus
        {
            HasAppCredentials = !string.IsNullOrWhiteSpace(clientId)
                && !string.IsNullOrWhiteSpace(await credentials.GetSecretAsync(Scope, ClientSecretKey, cancellationToken)),
            IsSignedIn = !string.IsNullOrWhiteSpace(access) || !string.IsNullOrWhiteSpace(refresh),
            AccessTokenExpiresAt = expires,
            RedirectUri = await GetRedirectUriAsync(cancellationToken),
            ClientIdHint = Mask(clientId)
        };
    }

    public async Task SaveAppCredentialsAsync(
        string clientId,
        string clientSecret,
        string? redirectUri,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Yahoo Client ID and Client Secret are both required.");

        await credentials.SaveSecretAsync(Scope, ClientIdKey, clientId.Trim(), cancellationToken);
        await credentials.SaveSecretAsync(Scope, ClientSecretKey, clientSecret.Trim(), cancellationToken);
        var uri = string.IsNullOrWhiteSpace(redirectUri) ? YahooAuthDefaults.RedirectUri : redirectUri.Trim();
        await credentials.SaveSecretAsync(Scope, RedirectUriKey, uri, cancellationToken);
    }

    public async Task ClearAppCredentialsAsync(CancellationToken cancellationToken = default)
    {
        await SignOutAsync(cancellationToken);
        await credentials.DeleteSecretAsync(Scope, ClientIdKey, cancellationToken);
        await credentials.DeleteSecretAsync(Scope, ClientSecretKey, cancellationToken);
        await credentials.DeleteSecretAsync(Scope, RedirectUriKey, cancellationToken);
    }

    public async Task<YahooSignInStart> StartSignInAsync(CancellationToken cancellationToken = default)
    {
        var (clientId, _, redirectUri) = await RequireAppAsync(cancellationToken);
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        await credentials.SaveSecretAsync(Scope, PendingStateKey, state, cancellationToken);
        var url = oauth.BuildAuthorizationUrl(clientId, redirectUri, state);
        var listening = CanListen(redirectUri);

        if (listening)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var code = await YahooRedirectListener.WaitForCodeAsync(redirectUri, state, TimeSpan.FromMinutes(3), cancellationToken);
                    if (!string.IsNullOrWhiteSpace(code))
                        await CompleteSignInAsync(code, state, cancellationToken);
                }
                catch (Exception)
                {
                    // The UI can fall back to a pasted authorization code.
                }
            }, cancellationToken);
        }

        TryOpenBrowser(url);
        return new YahooSignInStart
        {
            AuthorizationUrl = url,
            RedirectUri = redirectUri,
            State = state,
            LocalCallbackListening = listening
        };
    }

    public async Task CompleteSignInAsync(string authorizationCode, string? state, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(authorizationCode))
            throw new InvalidOperationException("Paste the Yahoo authorization code first.");

        var pending = await credentials.GetSecretAsync(Scope, PendingStateKey, cancellationToken);
        if (!string.IsNullOrWhiteSpace(pending)
            && !string.IsNullOrWhiteSpace(state)
            && !string.Equals(pending, state, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Yahoo sign-in state did not match. Start sign-in again.");
        }

        var (clientId, clientSecret, redirectUri) = await RequireAppAsync(cancellationToken);
        var token = await oauth.ExchangeCodeAsync(clientId, clientSecret, redirectUri, authorizationCode.Trim(), cancellationToken);
        await StoreTokenAsync(token, cancellationToken);
        await credentials.DeleteSecretAsync(Scope, PendingStateKey, cancellationToken);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        await credentials.DeleteSecretAsync(Scope, AccessTokenKey, cancellationToken);
        await credentials.DeleteSecretAsync(Scope, RefreshTokenKey, cancellationToken);
        await credentials.DeleteSecretAsync(Scope, ExpiresAtKey, cancellationToken);
        await credentials.DeleteSecretAsync(Scope, PendingStateKey, cancellationToken);
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        var access = await credentials.GetSecretAsync(Scope, AccessTokenKey, cancellationToken);
        var expiresRaw = await credentials.GetSecretAsync(Scope, ExpiresAtKey, cancellationToken);
        var stillValid = DateTimeOffset.TryParse(expiresRaw, out var expires) && expires > DateTimeOffset.UtcNow.AddMinutes(1);
        if (!string.IsNullOrWhiteSpace(access) && stillValid)
            return access;

        var refresh = await credentials.GetSecretAsync(Scope, RefreshTokenKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(refresh))
            throw new InvalidOperationException("Sign in to Yahoo first.");

        var (clientId, clientSecret, _) = await RequireAppAsync(cancellationToken);
        var token = await oauth.RefreshAsync(clientId, clientSecret, refresh, cancellationToken);
        await StoreTokenAsync(token, cancellationToken);
        return token.AccessToken!;
    }

    private async Task StoreTokenAsync(YahooTokenResponse token, CancellationToken cancellationToken)
    {
        await credentials.SaveSecretAsync(Scope, AccessTokenKey, token.AccessToken!, cancellationToken);
        if (!string.IsNullOrWhiteSpace(token.RefreshToken))
            await credentials.SaveSecretAsync(Scope, RefreshTokenKey, token.RefreshToken, cancellationToken);
        var lifetime = token.ExpiresIn > 0 ? token.ExpiresIn : 3600;
        var expires = DateTimeOffset.UtcNow.AddSeconds(lifetime - 60);
        await credentials.SaveSecretAsync(Scope, ExpiresAtKey, expires.ToString("O"), cancellationToken);
    }

    private async Task<(string ClientId, string ClientSecret, string RedirectUri)> RequireAppAsync(CancellationToken cancellationToken)
    {
        var clientId = await credentials.GetSecretAsync(Scope, ClientIdKey, cancellationToken);
        var clientSecret = await credentials.GetSecretAsync(Scope, ClientSecretKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            throw new InvalidOperationException(
                "Save a Yahoo Client ID and Client Secret first. Apply at sports.yahoo.com/developer/access/.");
        }

        return (clientId, clientSecret, await GetRedirectUriAsync(cancellationToken));
    }

    private async Task<string> GetRedirectUriAsync(CancellationToken cancellationToken)
    {
        var saved = await credentials.GetSecretAsync(Scope, RedirectUriKey, cancellationToken);
        return string.IsNullOrWhiteSpace(saved) ? YahooAuthDefaults.RedirectUri : saved;
    }

    private static bool CanListen(string redirectUri) =>
        Uri.TryCreate(redirectUri, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttp
        && (uri.Host is "127.0.0.1" or "localhost");

    private static void TryOpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception)
        {
        }
    }

    private static string? Mask(string? clientId)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return null;
        if (clientId.Length <= 8)
            return "••••";
        return $"{clientId[..4]}…{clientId[^4..]}";
    }
}
