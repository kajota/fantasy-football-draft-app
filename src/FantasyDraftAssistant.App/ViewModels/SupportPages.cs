using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class DraftOrderViewModel(ILeagueService leagues, IDraftCommandService commands, IDraftStateService drafts, SessionState session) : PageViewModel
{
    public ObservableCollection<string> Slots { get; } = [];

    [RelayCommand]
    private async Task CreateDraftAsync()
    {
        if (session.LeagueId is not { } leagueId)
        {
            StatusMessage = "Select a league first.";
            return;
        }

        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = leagueId,
            Name = $"{session.LeagueName ?? "League"} Draft"
        });
        session.DraftId = draft.DraftId;
        session.DraftName = draft.Name;
        session.BranchId = draft.ActiveBranchId;
        await OnNavigatedToAsync();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (session.DraftId is not { } id)
            return;
        var result = await commands.StartDraftAsync(new StartDraftCommand(id));
        StatusMessage = result.Succeeded ? "Draft started." : result.Error;
    }

    public override async Task OnNavigatedToAsync()
    {
        Title = "Draft Order";
        Slots.Clear();
        if (session.DraftId is not { } id)
        {
            StatusMessage = "Create a draft to generate slots.";
            return;
        }

        var working = await drafts.GetWorkingStateAsync(id);
        if (working is null)
            return;
        foreach (var slot in working.Slots)
        {
            var team = working.Teams.First(t => t.TeamId.Equals(slot.TeamId));
            Slots.Add($"{slot.Round}.{slot.RoundPick:00}  {team.Label}");
        }
    }
}

public partial class KeepersViewModel(ILeagueService leagues, IDraftStateService drafts, SessionState session) : PageViewModel
{
    public ObservableCollection<string> Keepers { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        Title = "Keepers";
        Keepers.Clear();
        if (session.DraftId is not { } id)
        {
            StatusMessage = "Create a draft before assigning keepers.";
            return;
        }

        foreach (var keeper in await leagues.GetKeepersAsync(id))
        {
            var players = await drafts.GetPlayersAsync();
            var player = players.FirstOrDefault(p => p.PlayerId.Equals(keeper.PlayerId));
            Keepers.Add($"{player?.Name ?? keeper.PlayerId.ToString()} — round {keeper.RoundCost}");
        }
    }
}

public partial class DataSourcesViewModel(IFantasyDataProvider seed, IFantasyDataWriter writer) : PageViewModel
{
    public ObservableCollection<string> Rows { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        Title = "Player Data";
        await RefreshListAsync();
    }

    [RelayCommand]
    private async Task RefreshSeedAsync()
    {
        var result = await seed.RefreshAsync(new FantasyDataRefreshRequest(), CancellationToken.None);
        StatusMessage = result.Succeeded
            ? $"Cached {result.PlayersWritten} players, {result.RankingsWritten} rankings."
            : result.Error;
        await RefreshListAsync();
    }

    private async Task RefreshListAsync()
    {
        Rows.Clear();
        foreach (var info in await writer.GetRefreshInfoAsync())
            Rows.Add($"{info.Dataset}: {info.RecordCount} rows at {info.RefreshedAt:g}");
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

public partial class HistoryViewModel(IDraftStateService drafts, SessionState session) : PageViewModel
{
    public ObservableCollection<string> Events { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        Title = "Draft History";
        Events.Clear();
        if (session.DraftId is not { } id)
            return;
        var state = await drafts.GetWorkingStateAsync(id);
        if (state is null)
            return;
        foreach (var selection in state.ActiveSelections.Values.OrderBy(s => s.OverallPick))
            Events.Add($"{selection.Round}.{selection.RoundPick:00}  {selection.PlayerId}  ({selection.Source})");
    }
}

public partial class BranchesViewModel(ILeagueService leagues, IDraftCommandService commands, SessionState session) : PageViewModel
{
    public ObservableCollection<string> Branches { get; } = [];
    [ObservableProperty] private string _newBranchName = "What-if";
    [ObservableProperty] private int _branchPoint = 1;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Branches";
        Branches.Clear();
        if (session.DraftId is not { } id)
            return;
        foreach (var branch in await leagues.GetBranchesAsync(id))
            Branches.Add($"{branch.Name}  (from pick {branch.BranchPointOverallPick})");
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (session.DraftId is not { } id)
            return;
        var result = await commands.CreateBranchAsync(new CreateDraftBranchCommand(id, NewBranchName, BranchPoint));
        StatusMessage = result.Succeeded ? "Branch created." : result.Error;
        if (result.Succeeded && result.BranchId is { } branchId)
            session.BranchId = branchId;
        await OnNavigatedToAsync();
    }
}

public partial class RecapViewModel(IAnalyticsService analytics, IDraftStateService drafts, SessionState session) : PageViewModel
{
    public ObservableCollection<string> Lines { get; } = [];

    public override async Task OnNavigatedToAsync()
    {
        Title = "Post-Draft Recap";
        Lines.Clear();
        if (session.DraftId is not { } id)
            return;
        var state = await drafts.GetWorkingStateAsync(id);
        if (state is null)
            return;
        var snapshot = await analytics.GetSnapshotAsync(id);
        Lines.Add($"Status: {state.Draft.Status}");
        Lines.Add($"Picks recorded: {state.ActiveSelections.Count}");
        Lines.Add($"QB demand: {snapshot.QbDemand} (superflex/multi-QB: {snapshot.ElevatedQbDemand})");
        foreach (var kv in snapshot.DraftedByPosition)
            Lines.Add($"Drafted {kv.Key}: {kv.Value}");
        if (state.Draft.Status != DraftStatus.Completed)
            Lines.Add("Complete the draft to generate the full recap. Deterministic totals are shown above even if AI is offline.");
    }
}
