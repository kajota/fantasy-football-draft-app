using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    public ShellViewModel(IServiceProvider services, SessionState session)
    {
        _services = services;
        Session = session;
        CurrentPage = services.GetRequiredService<LeaguesViewModel>();
        _ = CurrentPage.OnNavigatedToAsync();
    }

    public SessionState Session { get; }

    [ObservableProperty] private PageViewModel _currentPage = null!;
    [ObservableProperty] private string _activeNav = "leagues";

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
