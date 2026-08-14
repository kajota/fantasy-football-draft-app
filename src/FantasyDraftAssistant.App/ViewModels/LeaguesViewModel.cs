using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class LeaguesViewModel(
    ILeagueService leagues,
    IDraftCommandService commands,
    IFantasyDataProvider seed,
    SessionState session,
    Navigator navigator) : PageViewModel
{
    public ObservableCollection<LeagueSummary> Leagues { get; } = [];

    [ObservableProperty] private string _newLeagueName = "Sunday League";
    [ObservableProperty] private bool _newLeagueSuperflex;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Leagues";
        Leagues.Clear();
        foreach (var league in await leagues.ListLeaguesAsync())
            Leagues.Add(league);
    }

    [RelayCommand]
    private async Task CreateLeagueAsync()
    {
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = string.IsNullOrWhiteSpace(NewLeagueName) ? "New League" : NewLeagueName.Trim(),
            Season = 2026,
            TeamCount = 12,
            DraftType = DraftType.Snake,
            Superflex = NewLeagueSuperflex,
            RoundCount = NewLeagueSuperflex ? 16 : 15,
            UserTeamName = "My Team"
        });
        session.LeagueId = league.LeagueId;
        session.LeagueName = league.Name;
        session.DraftId = null;
        await OnNavigatedToAsync();
        await navigator.GoSetupAsync();
    }

    [RelayCommand]
    private async Task OpenLeagueAsync(LeagueSummary? summary)
    {
        if (summary is null)
            return;
        session.LeagueId = summary.LeagueId;
        session.LeagueName = summary.Name;
        session.DraftId = summary.ActiveDraftId;
        await navigator.GoSetupAsync();
    }

    [RelayCommand]
    private async Task StartMockDraftAsync()
    {
        StatusMessage = "Loading seed player data...";
        var refresh = await seed.RefreshAsync(new Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        if (!refresh.Succeeded)
        {
            StatusMessage = refresh.Error;
            return;
        }

        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Mock Superflex Draft",
            Season = 2026,
            TeamCount = 12,
            DraftType = DraftType.Snake,
            Superflex = true,
            RoundCount = 16,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Mock Draft"
        });
        var started = await commands.StartDraftAsync(new StartDraftCommand(draft.DraftId));
        if (!started.Succeeded)
        {
            StatusMessage = started.Error;
            return;
        }

        session.LeagueId = league.LeagueId;
        session.LeagueName = league.Name;
        session.DraftId = draft.DraftId;
        session.DraftName = draft.Name;
        session.BranchId = draft.ActiveBranchId;
        await navigator.GoRoomAsync();
    }
}
