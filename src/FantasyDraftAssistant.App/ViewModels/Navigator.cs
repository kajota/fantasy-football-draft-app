using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.App.ViewModels;

public sealed class Navigator(IServiceProvider services)
{
    public Task GoLeaguesAsync() => Resolve().GoLeaguesAsync();
    public Task GoSetupAsync() => Resolve().GoSetupAsync();
    public Task GoOrderAsync() => Resolve().GoOrderAsync();
    public Task GoKeepersAsync() => Resolve().GoKeepersAsync();
    public Task GoYahooAsync() => Resolve().GoYahooAsync();
    public Task GoDataAsync() => Resolve().GoDataAsync();
    public Task GoAiAsync() => Resolve().GoAiAsync();
    public Task GoReadyAsync() => Resolve().GoReadyAsync();
    public Task GoRoomAsync() => Resolve().GoRoomAsync();
    public Task GoHistoryAsync() => Resolve().GoHistoryAsync();
    public Task GoBranchesAsync() => Resolve().GoBranchesAsync();
    public Task GoRecapAsync() => Resolve().GoRecapAsync();

    private ShellViewModel Resolve() => services.GetRequiredService<ShellViewModel>();
}
