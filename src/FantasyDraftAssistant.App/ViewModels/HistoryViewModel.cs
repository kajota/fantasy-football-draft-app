using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.App.ViewModels;

public sealed class HistoryPickRow
{
    public required string RoundPick { get; init; }
    public required string Team { get; init; }
    public string TeamKey { get; init; } = "";
    public required string Player { get; init; }
    public required string Position { get; init; }
    public required string NflTeam { get; init; }
    public required string Source { get; init; }
}

public partial class HistoryViewModel(
    IDraftStateService drafts,
    IDraftQueryService queries,
    SessionState session,
    Navigator navigator) : PageViewModel
{
    public ObservableCollection<HistoryPickRow> Picks { get; } = [];

    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _hasPicks;
    [ObservableProperty] private string _summary = "";

    public override async Task OnNavigatedToAsync()
    {
        Title = "Draft History";
        Picks.Clear();
        HasDraft = false;
        HasPicks = false;
        Summary = "";

        if (session.DraftId is not { } draftId)
        {
            StatusMessage = "Open a draft first. History lists the active timeline for that draft.";
            return;
        }

        var state = await drafts.GetWorkingStateAsync(draftId, session.BranchId);
        if (state is null)
        {
            StatusMessage = "That draft was not found.";
            return;
        }

        HasDraft = true;
        session.BranchId = state.ActiveBranch.BranchId;
        var board = await queries.GetDraftBoardAsync(new QueryContext
        {
            DraftId = draftId,
            BranchId = state.ActiveBranch.BranchId
        });

        foreach (var pick in board.Picks)
        {
            Picks.Add(new HistoryPickRow
            {
                RoundPick = pick.RoundPick,
                Team = pick.Team,
                TeamKey = pick.TeamId,
                Player = pick.Player,
                Position = pick.Position,
                NflTeam = pick.NflTeam,
                Source = pick.Source
            });
        }

        HasPicks = Picks.Count > 0;
        Summary = $"{state.League.Name} · {state.ActiveBranch.Name} · {Picks.Count} pick(s)";
        StatusMessage = HasPicks
            ? "Active timeline only. Rolled-back picks stay in the event log but are not listed here."
            : "No picks on the active timeline yet.";
    }

    [RelayCommand]
    private Task OpenRoomAsync() => navigator.GoRoomAsync();
}
