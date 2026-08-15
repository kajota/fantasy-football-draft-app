using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.App.ViewModels;

public sealed class YahooLeagueRow
{
    public required string LeagueKey { get; init; }
    public required string Name { get; init; }
    public required string Summary { get; init; }
    public required bool CanImport { get; init; }
    public required string ActionLabel { get; init; }
}

public partial class YahooViewModel(
    IYahooAuthService auth,
    IYahooLeagueImporter importer,
    ICredentialStore credentials,
    SessionState session,
    Navigator navigator) : PageViewModel
{
    public ObservableCollection<YahooLeagueRow> Leagues { get; } = [];
    public ObservableCollection<string> ReviewItems { get; } = [];

    [ObservableProperty] private string _clientId = "";
    [ObservableProperty] private string _clientSecret = "";
    [ObservableProperty] private string _redirectUri = YahooAuthDefaults.RedirectUri;
    [ObservableProperty] private bool _hasSavedSecret;
    [ObservableProperty] private bool _isSignedIn;
    [ObservableProperty] private string _accountStatus = "Not signed in.";
    [ObservableProperty] private string _storeDescription = "";
    [ObservableProperty] private string _authorizationCode = "";
    [ObservableProperty] private string? _pendingState;
    [ObservableProperty] private bool _replaceDraftOrder;
    [ObservableProperty] private bool _hasLeagues;
    [ObservableProperty] private bool _hasReview;
    [ObservableProperty] private string _attribution = YahooAuthDefaults.AttributionText;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Yahoo";
        StoreDescription = credentials.Description;
        await RefreshStatusAsync();
    }

    [RelayCommand]
    private async Task SaveCredentialsAsync()
    {
        try
        {
            await auth.SaveAppCredentialsAsync(ClientId.Trim(), ClientSecret.Trim(), RedirectUri.Trim());
            ClientSecret = "";
            HasSavedSecret = true;
            StatusMessage = "Yahoo app credentials saved. They are not stored in the draft database.";
            await RefreshStatusAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        try
        {
            StatusMessage = "Opening Yahoo sign-in…";
            var start = await auth.StartSignInAsync();
            PendingState = start.State;
            StatusMessage = start.LocalCallbackListening
                ? "Finish signing in in the browser. This page will pick up the redirect. If it does not, paste the code from the address bar."
                : $"Open this URL, then paste the authorization code: {start.AuthorizationUrl}";
            await RefreshStatusAsync();
            if (start.LocalCallbackListening)
                _ = WaitForBrowserSignInAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task WaitForBrowserSignInAsync()
    {
        for (var i = 0; i < 45; i++)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            var status = await auth.GetStatusAsync();
            if (!status.IsSignedIn)
                continue;
            await RefreshStatusAsync();
            await RefreshLeaguesAsync();
            return;
        }
    }

    [RelayCommand]
    private async Task ApplyCodeAsync()
    {
        try
        {
            await auth.CompleteSignInAsync(AuthorizationCode, PendingState);
            AuthorizationCode = "";
            StatusMessage = "Yahoo account connected.";
            await RefreshStatusAsync();
            await RefreshLeaguesAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task SignOutAsync()
    {
        await auth.SignOutAsync();
        Leagues.Clear();
        HasLeagues = false;
        StatusMessage = "Signed out of Yahoo. App credentials are still saved.";
        await RefreshStatusAsync();
    }

    [RelayCommand]
    private async Task RefreshLeaguesAsync()
    {
        try
        {
            StatusMessage = "Loading Yahoo leagues…";
            var items = await importer.ListLeaguesAsync();
            Leagues.Clear();
            foreach (var item in items)
            {
                var parts = new List<string> { $"Season {item.Season}", $"{item.TeamCount} teams" };
                if (item.IsKeeper)
                    parts.Add("keeper");
                if (item.AlreadyImported)
                    parts.Add("already imported");
                if (item.IsAuction)
                    parts.Add("auction — cannot import");

                Leagues.Add(new YahooLeagueRow
                {
                    LeagueKey = item.LeagueKey,
                    Name = item.Name,
                    Summary = string.Join(" · ", parts),
                    CanImport = !item.IsAuction,
                    ActionLabel = item.IsAuction ? "Auction" : item.AlreadyImported ? "Refresh import" : "Import"
                });
            }

            HasLeagues = Leagues.Count > 0;
            StatusMessage = HasLeagues
                ? $"Found {Leagues.Count} Yahoo football league(s)."
                : "No Yahoo football leagues were returned for this account.";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task ImportLeagueAsync(YahooLeagueRow? row)
    {
        if (row is null || !row.CanImport)
            return;

        try
        {
            StatusMessage = $"Importing {row.Name}…";
            var result = await importer.ImportAsync(row.LeagueKey, new YahooImportOptions
            {
                ReplaceDraftOrder = ReplaceDraftOrder
            });
            if (!result.Succeeded || result.LeagueId is not { } leagueId)
            {
                StatusMessage = result.Error ?? "Yahoo import failed.";
                return;
            }

            session.LeagueId = leagueId;
            session.LeagueName = result.LeagueName;
            session.DraftId = null;
            session.BranchId = null;
            session.DraftName = null;

            ReviewItems.Clear();
            foreach (var item in result.ReviewItems)
                ReviewItems.Add(item);
            HasReview = ReviewItems.Count > 0;
            StatusMessage = result.CreatedNew
                ? $"Imported \"{result.LeagueName}\". Review the flagged settings, then continue on League Setup."
                : $"Updated \"{result.LeagueName}\" from Yahoo. Draft order and keepers were left alone unless you checked replace.";
            await navigator.GoSetupAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RefreshStatusAsync()
    {
        var status = await auth.GetStatusAsync();
        RedirectUri = status.RedirectUri;
        HasSavedSecret = status.HasAppCredentials;
        IsSignedIn = status.IsSignedIn;
        AccountStatus = status.IsSignedIn
            ? status.AccessTokenExpiresAt is { } expires
                ? $"Signed in. Token refresh scheduled around {expires.ToLocalTime():g}."
                : "Signed in."
            : status.HasAppCredentials
                ? "App credentials are saved. Sign in to list leagues."
                : "Save a Yahoo developer Client ID and secret first.";
        if (status.HasAppCredentials && string.IsNullOrWhiteSpace(ClientId) && status.ClientIdHint is not null)
            ClientId = status.ClientIdHint;
    }
}
