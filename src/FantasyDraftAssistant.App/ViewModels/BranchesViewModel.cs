using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class BranchRow : ObservableObject
{
    public required BranchId BranchId { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public required string PickSummary { get; init; }
    [ObservableProperty] private bool _isActive;
    public bool CanSwitch => !IsActive;
}

public partial class BranchesViewModel(
    ILeagueService leagues,
    IDraftCommandService commands,
    IDraftStateService drafts,
    IMockDraftService mock,
    SessionState session,
    Navigator navigator) : PageViewModel
{
    public ObservableCollection<BranchRow> Branches { get; } = [];

    [ObservableProperty] private string _newBranchName = "What-if";
    [ObservableProperty] private string _branchPointText = "1";
    [ObservableProperty] private bool _hasInvalidBranchPoint;
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private string _activeSummary = "";

    public override async Task OnNavigatedToAsync()
    {
        Title = "Branches";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task CreateAsync()
    {
        if (session.DraftId is not { } id)
        {
            StatusMessage = "Open or start a draft first.";
            return;
        }

        if (!TryParseBranchPoint(out var point))
        {
            StatusMessage = "From pick must be a whole number, for example 3, not 3.1. That is the overall pick where the what-if starts.";
            return;
        }

        var name = string.IsNullOrWhiteSpace(NewBranchName) ? "What-if" : NewBranchName.Trim();
        var result = await commands.CreateBranchAsync(new CreateDraftBranchCommand(id, name, point));
        if (!result.Succeeded)
        {
            StatusMessage = result.Error;
            return;
        }

        if (result.BranchId is { } branchId)
            session.BranchId = branchId;
        StatusMessage = $"Created \"{name}\" and switched to it. Open Draft Room to continue from pick {point}.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task SwitchAsync(BranchRow? row)
    {
        if (row is null || session.DraftId is not { } id)
            return;
        if (row.IsActive)
        {
            StatusMessage = $"Already on \"{row.Name}\".";
            return;
        }

        var result = await commands.SwitchBranchAsync(new SwitchDraftBranchCommand(id, row.BranchId));
        if (!result.Succeeded)
        {
            StatusMessage = result.Error;
            return;
        }

        session.BranchId = row.BranchId;
        StatusMessage = $"Switched to \"{row.Name}\". Open Draft Room to see that timeline.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task PracticeFromHereAsync()
    {
        if (session.DraftId is not { } id)
        {
            StatusMessage = "Open or start a draft first.";
            return;
        }

        var result = await mock.StartPracticeAsync(id);
        if (result.BranchId is { } branchId)
            session.BranchId = branchId;
        StatusMessage = result.Succeeded
            ? "Started a practice branch. Open Draft Room and use Play until my pick."
            : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private Task OpenRoomAsync() => navigator.GoRoomAsync();

    partial void OnBranchPointTextChanged(string value) =>
        HasInvalidBranchPoint = !TryParseBranchPoint(out _);

    private async Task ReloadAsync()
    {
        Branches.Clear();
        HasDraft = false;
        ActiveSummary = "";
        HasInvalidBranchPoint = !TryParseBranchPoint(out _);

        if (session.DraftId is not { } id)
        {
            StatusMessage ??= "Open or start a draft first. Branches are what-if timelines of that draft.";
            return;
        }

        var draft = await leagues.GetDraftAsync(id);
        if (draft is null)
        {
            StatusMessage = "Open or start a draft first. Branches are what-if timelines of that draft.";
            return;
        }

        HasDraft = true;
        var stored = await leagues.GetBranchesAsync(id);
        if (session.BranchId is not { } sessionBranch || stored.All(b => !b.BranchId.Equals(sessionBranch)))
            session.BranchId = draft.ActiveBranchId;
        foreach (var branch in stored
                     .OrderBy(b => b.ParentBranchId is null ? 0 : 1)
                     .ThenBy(b => b.CreatedAt))
        {
            var state = await drafts.GetWorkingStateAsync(id, branch.BranchId);
            var pickCount = state?.ActiveSelections.Count ?? 0;
            Branches.Add(new BranchRow
            {
                BranchId = branch.BranchId,
                Name = branch.Name,
                Detail = Describe(branch),
                PickSummary = pickCount == 1 ? "1 pick on this timeline" : $"{pickCount} picks on this timeline",
                IsActive = branch.BranchId.Equals(draft.ActiveBranchId)
            });
        }

        var active = Branches.FirstOrDefault(b => b.IsActive);
        ActiveSummary = active is null
            ? "No active branch."
            : $"Active: {active.Name}. {active.PickSummary}.";

        var working = await drafts.GetWorkingStateAsync(id, draft.ActiveBranchId);
        if (working is not null && TryParseBranchPoint(out var currentText) && currentText == 1)
            BranchPointText = Math.Max(1, working.CurrentOverallPick).ToString(CultureInfo.InvariantCulture);

        if (string.IsNullOrWhiteSpace(StatusMessage))
            StatusMessage = "Create a branch to replay from a pick without changing the main timeline. Switch to open that timeline in Draft Room.";
    }

    private bool TryParseBranchPoint(out int point) =>
        int.TryParse(BranchPointText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out point)
        && point >= 1;

    private static string Describe(DraftBranch branch)
    {
        if (branch.ParentBranchId is null || branch.BranchPointOverallPick <= 0)
            return "Main timeline";

        return $"What-if from overall pick {branch.BranchPointOverallPick}. Picks before that stay shared.";
    }
}
