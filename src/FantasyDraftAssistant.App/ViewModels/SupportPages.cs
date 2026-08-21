using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Providers.FantasyData;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class FantasyDataProviderRow : ObservableObject
{
    public required string ProviderKey { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public bool NeedsApiKey { get; init; }
    [ObservableProperty] private string _apiKey = "";
    [ObservableProperty] private bool _keySaved;
    [ObservableProperty] private string _lastSynced = "Not synced yet.";
}

public partial class DataSourcesViewModel(
    IFantasyDataProviderRegistry registry,
    IFantasyDataWriter writer,
    ICredentialStore credentials,
    ILeagueService leagues,
    SessionState session) : PageViewModel
{
    public ObservableCollection<FantasyDataProviderRow> Providers { get; } = [];
    public ObservableCollection<string> Rows { get; } = [];

    [ObservableProperty] private string _cacheTimeZoneNote = LocalClock.ZoneNote();

    public override async Task OnNavigatedToAsync()
    {
        Title = "Player Data";
        Providers.Clear();
        foreach (var provider in registry.All)
        {
            var row = new FantasyDataProviderRow
            {
                ProviderKey = provider.ProviderKey,
                Title = provider.DisplayName,
                Description = provider.Description,
                NeedsApiKey = provider.ProviderKey.Equals(FantasyProsFantasyDataProvider.Key, StringComparison.OrdinalIgnoreCase)
            };
            if (row.NeedsApiKey)
            {
                var secret = await credentials.GetSecretAsync(
                    FantasyProsFantasyDataProvider.CredentialScope,
                    FantasyProsFantasyDataProvider.CredentialKey);
                row.KeySaved = !string.IsNullOrWhiteSpace(secret);
            }

            Providers.Add(row);
        }

        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task SaveKeyAsync(FantasyDataProviderRow? row)
    {
        if (row is null || !row.NeedsApiKey)
            return;
        if (string.IsNullOrWhiteSpace(row.ApiKey))
        {
            StatusMessage = "Paste the FantasyPros API key first.";
            return;
        }

        var wasSaved = row.KeySaved;
        await credentials.SaveSecretAsync(
            FantasyProsFantasyDataProvider.CredentialScope,
            FantasyProsFantasyDataProvider.CredentialKey,
            row.ApiKey.Trim());
        row.ApiKey = "";
        row.KeySaved = true;
        StatusMessage = wasSaved
            ? "FantasyPros key updated. It is not stored in the draft database."
            : "FantasyPros key saved. It is not stored in the draft database.";
    }

    [RelayCommand]
    private async Task RefreshProviderAsync(FantasyDataProviderRow? row)
    {
        if (row is null)
            return;
        if (row.NeedsApiKey && !string.IsNullOrWhiteSpace(row.ApiKey))
        {
            await credentials.SaveSecretAsync(
                FantasyProsFantasyDataProvider.CredentialScope,
                FantasyProsFantasyDataProvider.CredentialKey,
                row.ApiKey.Trim());
            row.ApiKey = "";
            row.KeySaved = true;
        }

        var provider = registry.Get(row.ProviderKey);
        if (provider is null)
        {
            StatusMessage = $"Provider {row.ProviderKey} is not registered.";
            return;
        }

        var format = await FormatForCurrentLeagueAsync();
        StatusMessage = $"Refreshing {row.Title} for {format.DisplayName}...";
        var result = await provider.RefreshAsync(new FantasyDataRefreshRequest
        {
            Scoring = format.Scoring,
            Superflex = format.Superflex
        }, CancellationToken.None);
        StatusMessage = result.Succeeded
            ? $"Cached {result.PlayersWritten} players, {result.RankingsWritten} ranks, {result.AdpWritten} ADP for {format.DisplayName}. FantasyPros sheets are Standard / Half PPR / PPR and 1-QB or Superflex — closest match, not custom scoring. Proj uses this league's exact rules."
            : result.Error;
        await RefreshListAsync();
    }

    private async Task RefreshListAsync()
    {
        Rows.Clear();
        CacheTimeZoneNote = LocalClock.ZoneNote();
        var format = await FormatForCurrentLeagueAsync();
        Rows.Add(session.LeagueId is null
            ? $"No league selected — FantasyPros refresh uses {format.DisplayName}."
            : $"Open league will use FantasyPros {format.DisplayName}.");

        var refreshes = await writer.GetRefreshInfoAsync();
        foreach (var info in refreshes)
            Rows.Add($"{info.ProviderKey} {info.Dataset}: {info.RecordCount} rows at {LocalClock.Format(info.RefreshedAt)}");

        var latestByProvider = refreshes
            .GroupBy(info => info.ProviderKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.Max(info => info.RefreshedAt),
                StringComparer.OrdinalIgnoreCase);

        foreach (var provider in Providers)
        {
            provider.LastSynced = latestByProvider.TryGetValue(provider.ProviderKey, out var stamp)
                ? $"Last synced {LocalClock.Format(stamp)}"
                : "Not synced yet.";
        }
    }

    private async Task<FantasyDataFormat> FormatForCurrentLeagueAsync()
    {
        if (session.LeagueId is not { } leagueId)
            return FantasyDataFormat.Default;

        var scoring = await leagues.GetScoringRulesAsync(leagueId);
        var roster = await leagues.GetRosterSlotsAsync(leagueId);
        if (scoring.Count == 0 && roster.Count == 0)
            return FantasyDataFormat.Default;
        return FantasyDataFormat.FromLeague(scoring, roster);
    }
}

public partial class ReadinessViewModel(IReadinessService readiness, SessionState session) : PageViewModel
{
    public ObservableCollection<ReadinessItem> Items { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        Title = "Draft Readiness";
        Items.Clear();
        foreach (var item in await readiness.CheckAsync(session.DraftId))
            Items.Add(item);
    }
}
