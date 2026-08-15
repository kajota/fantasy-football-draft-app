using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.App.ViewModels;

public sealed class RecapTeamBlock
{
    public required string Title { get; init; }
    public required string TeamKey { get; init; }
    public required string Detail { get; init; }
    public required IReadOnlyList<string> Players { get; init; }
}

public partial class RecapViewModel(
    IAnalyticsService analytics,
    IDraftStateService drafts,
    IDraftQueryService queries,
    IDraftCommandService commands,
    IFantasyDataWriter fantasyData,
    SessionState session,
    Navigator navigator) : PageViewModel
{
    public ObservableCollection<string> Summary { get; } = [];
    public ObservableCollection<string> ValueLines { get; } = [];
    public ObservableCollection<RecapTeamBlock> Teams { get; } = [];

    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _canComplete;
    [ObservableProperty] private bool _hasTeams;
    [ObservableProperty] private bool _hasValue;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Post-Draft Recap";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task CompleteDraftAsync()
    {
        if (session.DraftId is not { } draftId)
            return;

        var result = await commands.CompleteDraftAsync(new CompleteDraftCommand(draftId));
        StatusMessage = result.Succeeded
            ? "Draft marked complete. The numbers below do not need AI."
            : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private Task OpenRoomAsync() => navigator.GoRoomAsync();

    private async Task ReloadAsync()
    {
        Summary.Clear();
        ValueLines.Clear();
        Teams.Clear();
        HasDraft = false;
        CanComplete = false;
        HasTeams = false;
        HasValue = false;

        if (session.DraftId is not { } draftId)
        {
            StatusMessage = "Open a draft first. Recap uses the active timeline.";
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
        var context = new QueryContext
        {
            DraftId = draftId,
            BranchId = state.ActiveBranch.BranchId
        };
        var snapshot = await analytics.GetSnapshotAsync(draftId, state.ActiveBranch.BranchId);
        var board = await queries.GetDraftBoardAsync(context);
        var format = FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots);
        var sourceKey = FantasyDataSourcePicker.Pick(await fantasyData.GetSourceKeysAsync(), format);
        var adp = await fantasyData.GetAdpAsync(sourceKey);
        if (adp.Count == 0)
            adp = await fantasyData.GetAdpAsync();
        var players = (await drafts.GetPlayersAsync()).ToDictionary(p => p.PlayerId);

        CanComplete = state.Draft.Status == DraftStatus.InProgress && state.CurrentSlot is null;
        Summary.Add($"{state.League.Name} · {state.Draft.Status} · {state.ActiveBranch.Name}");
        Summary.Add($"{board.Picks.Count} of {state.Slots.Count} slots filled.");
        Summary.Add($"QB demand: {snapshot.QbDemand} (superflex/multi-QB: {snapshot.ElevatedQbDemand})");
        foreach (var kv in snapshot.DraftedByPosition.Where(kv => kv.Value > 0))
            Summary.Add($"Drafted {kv.Key}: {kv.Value}");

        if (state.League.UserTeamId is { } userTeam)
        {
            foreach (var selection in state.SelectionsForTeam(userTeam).OrderBy(s => s.OverallPick))
            {
                if (!players.TryGetValue(selection.PlayerId, out var player))
                    continue;
                if (!adp.TryGetValue(player.PlayerId, out var row) || row.OverallAdp <= 0)
                    continue;
                var consensus = AdpConverter.FormatRoundPick(row.OverallAdp, state.League.TeamCount);
                var delta = selection.OverallPick - row.OverallAdp;
                var label = delta <= -1.5 ? "reach"
                    : delta >= 1.5 ? "value"
                    : "on ADP";
                var pickLabel = $"{selection.Round}.{selection.RoundPick:00}";
                ValueLines.Add($"{pickLabel}  {player.Name}  ADP {consensus}  ({label})");
            }
        }

        HasValue = ValueLines.Count > 0;

        foreach (var team in state.Teams.OrderBy(t => t.DraftPosition))
        {
            var roster = await queries.GetTeamRosterAsync(context, team.TeamId);
            var isUser = state.League.UserTeamId is { } mine && team.TeamId.Equals(mine);
            Teams.Add(new RecapTeamBlock
            {
                Title = isUser ? $"{team.Label} (you)" : team.Label,
                TeamKey = team.TeamId.ToString(),
                Detail = roster.Players.Count == 0
                    ? "No picks yet."
                    : $"{roster.Players.Count} player(s)",
                Players = roster.Players
                    .Select(p => $"{p.RoundPick}  {p.Position}  {p.NflTeam}  {p.Name}")
                    .ToList()
            });
        }

        HasTeams = Teams.Count > 0;
        StatusMessage = state.Draft.Status == DraftStatus.Completed
            ? "Deterministic recap. Ask an AI provider in Draft Room if you want a narrative grade."
            : CanComplete
                ? "Every slot is filled. Mark the draft complete when you are done."
                : "Draft is still open. Names and totals below update as picks land.";
    }
}
