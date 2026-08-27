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

public sealed record YahooPasteTeamRow(int Seat, string Name, string Owner);

public sealed record YahooPasteSlotRow(string Slot, int Count);

public sealed record YahooPasteScoringRow(string Label, string Points);

public partial class YahooViewModel(
    IYahooAuthService auth,
    IYahooLeagueImporter importer,
    IYahooPasteImporter pasteImporter,
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

    // --- Paste import (no API key) ---------------------------------------

    private YahooLeagueSnapshot? _pasteSnapshot;

    public ObservableCollection<YahooPasteTeamRow> PasteTeams { get; } = [];
    public ObservableCollection<YahooPasteSlotRow> PasteRoster { get; } = [];
    public ObservableCollection<YahooPasteScoringRow> PasteScoring { get; } = [];
    public ObservableCollection<string> PasteWarnings { get; } = [];
    public ObservableCollection<string> PasteProblems { get; } = [];
    public ObservableCollection<YahooAiReaderOption> AiReaders { get; } = [];

    [ObservableProperty] private string _pasteLeagueUrl = "";
    [ObservableProperty] private string _pasteSettingsText = "";
    [ObservableProperty] private string _pasteTeamsText = "";
    [ObservableProperty] private string _pasteSummary = "";
    [ObservableProperty] private bool _hasPastePreview;
    [ObservableProperty] private bool _hasPasteWarnings;
    [ObservableProperty] private bool _hasPasteProblems;
    [ObservableProperty] private bool _canUseAi;
    [ObservableProperty] private YahooAiReaderOption? _selectedAiReader;
    [ObservableProperty] private bool _isReadingPaste;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Yahoo";
        StoreDescription = credentials.Description;
        await RefreshAiReadersAsync();
        await RefreshStatusAsync();
    }

    // --- Paste import commands -------------------------------------------

    private async Task RefreshAiReadersAsync()
    {
        var readers = await pasteImporter.ListAiReadersAsync();
        AiReaders.Clear();
        foreach (var reader in readers)
            AiReaders.Add(reader);

        // First entry is the Fast Advisor when one is set up — the role meant for
        // cheap, quick calls. The user can still pick a different one.
        SelectedAiReader = AiReaders.FirstOrDefault();
        CanUseAi = AiReaders.Count > 0;
    }

    [RelayCommand]
    private async Task ParsePasteAsync()
    {
        await ApplyParseAsync(() => Task.FromResult(pasteImporter.Parse(BuildPasteInput())));
    }

    [RelayCommand]
    private async Task ReadPasteWithAiAsync()
    {
        if (SelectedAiReader is not { } reader)
        {
            StatusMessage = "Enable a provider and save its API key on AI Providers first.";
            return;
        }

        StatusMessage = $"Asking {reader.DisplayName} ({reader.Model}) to read the paste…";
        await ApplyParseAsync(() => pasteImporter.ParseWithAiAsync(BuildPasteInput(), reader.ProviderKey));
    }

    private async Task ApplyParseAsync(Func<Task<YahooPasteParseResult>> parse)
    {
        if (string.IsNullOrWhiteSpace(PasteSettingsText) && string.IsNullOrWhiteSpace(PasteTeamsText))
        {
            StatusMessage = "Paste the Settings page and the Teams page first.";
            return;
        }

        IsReadingPaste = true;
        try
        {
            var result = await parse();

            PasteWarnings.Clear();
            foreach (var warning in result.Warnings)
                PasteWarnings.Add(warning);
            HasPasteWarnings = PasteWarnings.Count > 0;

            PasteProblems.Clear();
            foreach (var missing in result.MissingSections)
                PasteProblems.Add(missing);
            HasPasteProblems = PasteProblems.Count > 0;

            if (result.Snapshot is null)
            {
                ClearPastePreview();
                StatusMessage = result.UsedAi
                    ? "The AI could not read this paste. Fill the league in by hand on League Setup."
                    : "Could not read part of the paste. Try \"Let the AI read it\", or fill the league in by hand on League Setup.";
                return;
            }

            ShowPastePreview(result.Snapshot);
            StatusMessage = result.UsedAi
                ? $"Read by {SelectedAiReader?.DisplayName ?? "the AI"}. Check the preview carefully, then import."
                : "Parsed the paste. Check the preview, then import.";
        }
        catch (Exception ex)
        {
            ClearPastePreview();
            StatusMessage = ex.Message;
        }
        finally
        {
            IsReadingPaste = false;
        }
    }

    private void ShowPastePreview(YahooLeagueSnapshot snapshot)
    {
        // Preview what will actually be saved, i.e. after canonicalisation.
        var request = pasteImporter.Preview(snapshot).Mapped.Request;
        _pasteSnapshot = snapshot;

        PasteTeams.Clear();
        foreach (var team in request.Teams)
            PasteTeams.Add(new YahooPasteTeamRow(team.SuggestedDraftPosition, team.Name, team.OwnerName ?? "—"));

        PasteRoster.Clear();
        foreach (var slot in request.Roster)
            PasteRoster.Add(new YahooPasteSlotRow(slot.SlotCode, slot.Count));

        PasteScoring.Clear();
        foreach (var rule in request.Scoring.OrderBy(r => r.Category.ToString(), StringComparer.Ordinal))
            PasteScoring.Add(new YahooPasteScoringRow(Humanize(rule.Category.ToString()), rule.Points.ToString("0.####")));

        PasteSummary = $"{request.Name} · {request.Season} · {request.Teams.Count} teams · {request.RoundCount} rounds · {request.DraftType}";
        HasPastePreview = true;
    }

    private void ClearPastePreview()
    {
        _pasteSnapshot = null;
        PasteTeams.Clear();
        PasteRoster.Clear();
        PasteScoring.Clear();
        PasteSummary = "";
        HasPastePreview = false;
    }

    [RelayCommand]
    private async Task ImportPasteAsync()
    {
        if (_pasteSnapshot is null)
        {
            StatusMessage = "Parse the paste first.";
            return;
        }

        try
        {
            StatusMessage = "Importing…";
            var result = await pasteImporter.ImportAsync(_pasteSnapshot, new YahooImportOptions
            {
                ReplaceDraftOrder = ReplaceDraftOrder
            });
            await AfterImportAsync(result);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private YahooPasteInput BuildPasteInput() => new()
    {
        LeagueUrlOrId = string.IsNullOrWhiteSpace(PasteLeagueUrl) ? null : PasteLeagueUrl.Trim(),
        SettingsText = PasteSettingsText,
        TeamsText = PasteTeamsText
    };

    /// "PointsAllowed35Plus" -> "Points allowed 35 plus", "FieldGoal0To19" -> "Field goal 0 to 19".
    private static string Humanize(string category)
    {
        var builder = new System.Text.StringBuilder(category.Length + 8);
        for (var i = 0; i < category.Length; i++)
        {
            var ch = category[i];
            if (i == 0)
            {
                builder.Append(ch);
                continue;
            }

            if (char.IsUpper(ch))
                builder.Append(' ').Append(char.ToLowerInvariant(ch));
            else if (char.IsDigit(ch) && !char.IsDigit(category[i - 1]))
                builder.Append(' ').Append(ch);
            else
                builder.Append(ch);
        }

        return builder.ToString();
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
            await AfterImportAsync(result);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task AfterImportAsync(YahooImportResult result)
    {
        if (!result.Succeeded || result.LeagueId is not { } leagueId)
        {
            StatusMessage = result.Error ?? "Yahoo import failed.";
            return;
        }

        session.LeagueId = leagueId;
        session.LeagueName = result.LeagueName;
        SessionDraft.BindDraft(session, null);

        ReviewItems.Clear();
        foreach (var item in result.ReviewItems)
            ReviewItems.Add(item);
        HasReview = ReviewItems.Count > 0;
        StatusMessage = result.CreatedNew
            ? $"Imported \"{result.LeagueName}\". Review the flagged settings, then continue on League Setup."
            : $"Updated \"{result.LeagueName}\" from Yahoo. Draft order and keepers were left alone unless you checked replace.";
        await navigator.GoSetupAsync();
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
