using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Data.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public ShellViewModel(IServiceProvider services, SessionState session, DataLockService dataLock)
    {
        _services = services;
        Session = session;
        HasStolenLock = dataLock.WasStolen;
        dataLock.Stolen += (_, _) => Dispatcher.UIThread.Post(() => HasStolenLock = true);
        CurrentPage = services.GetRequiredService<LeaguesViewModel>();
        _ = CurrentPage.OnNavigatedToAsync();
    }

    public SessionState Session { get; }

    public string VersionLabel { get; } = $"v{AppVersion.Display}";

    public string VersionTooltip { get; } = AppVersion.Commit is { } sha
        ? $"Fantasy Draft Assistant {AppVersion.Display} (commit {sha})"
        : $"Fantasy Draft Assistant {AppVersion.Display}";

    [ObservableProperty] private PageViewModel _currentPage = null!;
    [ObservableProperty] private string _activeNav = "leagues";
    [ObservableProperty] private bool _hasStolenLock;

    public string StolenLockMessage { get; } =
        "Another computer took over this database. Stop using this copy. Two writers can corrupt your leagues.";

    [RelayCommand]
    public Task GoLeaguesAsync() => NavigateAsync<LeaguesViewModel>("leagues");

    [RelayCommand]
    public Task GoSetupAsync() => NavigateAsync<LeagueSetupViewModel>("setup");

    [RelayCommand]
    public Task GoOrderAsync() => NavigateAsync<DraftOrderViewModel>("order");

    [RelayCommand]
    public Task GoKeepersAsync() => NavigateAsync<KeepersViewModel>("keepers");

    [RelayCommand]
    public Task GoYahooAsync() => NavigateAsync<YahooViewModel>("yahoo");

    [RelayCommand]
    public Task GoDataAsync() => NavigateAsync<DataSourcesViewModel>("data");

    [RelayCommand]
    public Task GoAiAsync() => NavigateAsync<AiSettingsViewModel>("ai");

    [RelayCommand]
    public Task GoReadyAsync() => NavigateAsync<ReadinessViewModel>("ready");

    [RelayCommand]
    public Task GoRoomAsync() => NavigateAsync<DraftRoomViewModel>("room");

    [RelayCommand]
    public Task GoHistoryAsync() => NavigateAsync<HistoryViewModel>("history");

    [RelayCommand]
    public Task GoBranchesAsync() => NavigateAsync<BranchesViewModel>("branches");

    [RelayCommand]
    public Task GoRecapAsync() => NavigateAsync<RecapViewModel>("recap");

    public async Task NavigateAsync<T>(string nav) where T : PageViewModel
    {
        var page = _services.GetRequiredService<T>();
        CurrentPage = page;
        ActiveNav = nav;
        await page.OnNavigatedToAsync();
    }
}
