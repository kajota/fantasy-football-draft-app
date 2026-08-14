using CommunityToolkit.Mvvm.ComponentModel;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.App.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string? _statusMessage;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}

public sealed class SessionState
{
    public LeagueId? LeagueId { get; set; }
    public DraftId? DraftId { get; set; }
    public BranchId? BranchId { get; set; }
    public string? LeagueName { get; set; }
    public string? DraftName { get; set; }
}
