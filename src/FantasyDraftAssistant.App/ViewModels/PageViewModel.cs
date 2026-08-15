using CommunityToolkit.Mvvm.ComponentModel;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.App.ViewModels;

public abstract partial class PageViewModel : ObservableObject
{
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string? _statusMessage;

    public virtual Task OnNavigatedToAsync() => Task.CompletedTask;
}

public sealed partial class SessionState : ObservableObject
{
    [ObservableProperty] private LeagueId? _leagueId;
    [ObservableProperty] private DraftId? _draftId;
    [ObservableProperty] private BranchId? _branchId;
    [ObservableProperty] private string? _leagueName;
    [ObservableProperty] private string? _draftName;
    [ObservableProperty] private string? _dataSourceKey;

    public void ClearIfCurrentLeague(LeagueId leagueId)
    {
        if (LeagueId is not { } current || !current.Equals(leagueId))
            return;

        LeagueId = null;
        DraftId = null;
        BranchId = null;
        LeagueName = null;
        DraftName = null;
    }
}
