using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;

namespace FantasyDraftAssistant.App.ViewModels;

public sealed class RecapTeamBlock
{
    public required string Title { get; init; }
    public required string TeamKey { get; init; }
    public required string Grade { get; init; }
    public required string Headline { get; init; }
    public required string Detail { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }
    public required IReadOnlyList<string> Players { get; init; }
    public string Personality { get; init; } = "";

    /// An AI seat drafts to a strategy it keeps to itself for the whole draft. This is
    /// where it finally gets told - the recap is the first point where knowing it
    /// cannot change how you play against it.
    public string AiStrategy { get; init; } = "";

    /// The seat's own picks with the rationale it gave at the time.
    public IReadOnlyList<string> AiReasons { get; init; } = [];

    public bool HasNotes => Notes.Count > 0;
    public bool HasPersonality => Personality.Length > 0;
    public bool HasAiStrategy => AiStrategy.Length > 0;
    public bool HasAiReasons => AiReasons.Count > 0;
}

public partial class RecapViewModel(
    IAnalyticsService analytics,
    ILeagueService leagues,
    IDraftStateService drafts,
    IDraftQueryService queries,
    IDraftCommandService commands,
    IFantasyDataWriter fantasyData,
    IMockDraftService mock,
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

        if (session.LeagueId is { } leagueId)
            await SessionDraft.AttachLeagueAsync(session, leagues, leagueId, session.LeagueName);

        if (session.DraftId is not { } draftId)
        {
            StatusMessage = "Open a league first. Recap uses that league's active draft.";
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
        var rankings = await fantasyData.GetRankingsAsync(sourceKey);
        if (rankings.Count == 0)
            rankings = await fantasyData.GetRankingsAsync();
        var projections = await fantasyData.GetProjectionsAsync(sourceKey);
        if (projections.Count == 0)
            projections = await fantasyData.GetProjectionsAsync();
        var players = (await drafts.GetPlayersAsync()).ToDictionary(p => p.PlayerId);
        var playerList = players.Values.ToList();

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

        // Practice branches have seat policies; the live board has none, so
        // personality lines only appear on practice recaps.
        var policies = await mock.GetPoliciesAsync(draftId, state.ActiveBranch.BranchId);

        var playerNames = playerList.ToDictionary(player => player.PlayerId, player => player.Name);
        var reasonsByTeam = (await mock.GetPickReasonsAsync(draftId, state.ActiveBranch.BranchId))
            .GroupBy(reason => reason.TeamId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group
                    .OrderBy(reason => reason.OverallPick)
                    .Select(reason =>
                    {
                        var slot = state.Slots.FirstOrDefault(item => item.OverallPick == reason.OverallPick);
                        var label = slot is null ? $"#{reason.OverallPick}" : $"{slot.Round}.{slot.RoundPick:00}";
                        var name = state.ActiveSelections.TryGetValue(reason.OverallPick, out var selection)
                                   && playerNames.TryGetValue(selection.PlayerId, out var playerName)
                            ? playerName
                            : "";
                        var fallback = reason.UsedFallback ? " (fallback pick)" : "";
                        return $"{label}  {name}{fallback} — {reason.Reason}";
                    })
                    .ToList());

        var grades = DraftGrader.Grade(state, playerList, rankings, adp, projections);
        foreach (var grade in grades)
        {
            var roster = await queries.GetTeamRosterAsync(context, grade.TeamId);
            var policy = policies.FirstOrDefault(p => p.TeamId.Equals(grade.TeamId));
            Teams.Add(new RecapTeamBlock
            {
                Title = grade.IsUser ? $"{grade.TeamName} (you)" : grade.TeamName,
                TeamKey = grade.TeamId.ToString(),
                Grade = grade.Letter,
                Headline = grade.Headline,
                Detail = roster.Players.Count == 0
                    ? "No picks yet."
                    : $"{roster.Players.Count} player(s) · {grade.StarterPoints:0} starter pts",
                Notes = grade.Notes,
                Players = roster.Players
                    .Select(p => $"{p.RoundPick}  {p.Position}  {p.NflTeam}  {p.Name}")
                    .ToList(),
                Personality = policy is { IsCpu: true }
                    ? $"CPU personality: {MockPersonalityCatalog.Title(policy.Personality)}"
                    : "",
                AiStrategy = policy is { Personality: MockPersonality.Ai }
                    ? $"Hidden strategy: {MockAiStrategyCatalog.Find(policy.AiStrategy).Title}"
                    : "",
                AiReasons = reasonsByTeam.TryGetValue(grade.TeamId, out var teamReasons)
                    ? teamReasons
                    : []
            });
        }

        HasTeams = Teams.Count > 0;
        StatusMessage = state.Draft.Status == DraftStatus.Completed
            ? "Letter grades are relative to this league (size, Superflex, scoring). Refresh player data if ADP/projections look thin."
            : CanComplete
                ? "Every slot is filled. Grades below are live; mark complete when you are done."
                : "Draft is still open. Grades update as picks land.";
    }
}
