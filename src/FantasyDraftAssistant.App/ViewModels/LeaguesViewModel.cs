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
    SessionState session,
    Navigator navigator) : PageViewModel
{
    public ObservableCollection<LeagueSummary> Leagues { get; } = [];
    public ObservableCollection<LeagueSummary> ArchivedLeagues { get; } = [];

    public IReadOnlyList<int> TeamCountOptions { get; } = [8, 10, 12, 14, 16];

    [ObservableProperty] private string _newLeagueName = "Sunday League";
    [ObservableProperty] private int _newLeagueTeamCount = 12;
    [ObservableProperty] private bool _newLeagueSuperflex;
    [ObservableProperty] private bool _hasArchived;
    [ObservableProperty] private bool _hasRemovalDialog;
    [ObservableProperty] private bool _isConfirmingDelete;
    [ObservableProperty] private bool _canPermanentlyDelete;
    [ObservableProperty] private string _confirmName = "";
    [ObservableProperty] private string _removalTitle = "";
    [ObservableProperty] private string _removalWarning = "";
    [ObservableProperty] private LeagueSummary? _pendingLeague;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Leagues";
        await ReloadListsAsync();
    }

    [RelayCommand]
    private async Task CreateLeagueAsync()
    {
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = string.IsNullOrWhiteSpace(NewLeagueName) ? "New League" : NewLeagueName.Trim(),
            Season = 2026,
            TeamCount = TeamCountOptions.Contains(NewLeagueTeamCount) ? NewLeagueTeamCount : 12,
            DraftType = DraftType.Snake,
            Superflex = NewLeagueSuperflex,
            RoundCount = NewLeagueSuperflex ? 16 : 15,
            UserTeamName = "My Team"
        });
        SessionDraft.BindDraft(session, null);
        session.LeagueId = league.LeagueId;
        session.LeagueName = league.Name;
        await OnNavigatedToAsync();
        await navigator.GoSetupAsync();
    }

    [RelayCommand]
    private async Task OpenLeagueAsync(LeagueSummary? summary)
    {
        if (summary is null)
            return;
        await SessionDraft.AttachLeagueAsync(session, leagues, summary.LeagueId, summary.Name);
        await navigator.GoSetupAsync();
    }

    [RelayCommand]
    private void RequestRemove(LeagueSummary? summary)
    {
        if (summary is null)
            return;

        OpenRemovalDialog(summary, confirmDelete: false);
    }

    [RelayCommand]
    private void RequestPermanentDelete(LeagueSummary? summary)
    {
        if (summary is null)
            return;

        OpenRemovalDialog(summary, confirmDelete: true);
    }

    [RelayCommand]
    private void CancelRemoval()
    {
        CloseRemovalDialog();
    }

    [RelayCommand]
    private void BeginPermanentDelete()
    {
        if (PendingLeague is null)
            return;

        IsConfirmingDelete = true;
        ConfirmName = "";
        CanPermanentlyDelete = false;
        RemovalTitle = $"Permanently delete \"{PendingLeague.Name}\"?";
        RemovalWarning = BuildPermanentDeleteWarning(PendingLeague);
    }

    [RelayCommand]
    private void BackToRemovalChoices()
    {
        if (PendingLeague is not { } league)
            return;

        OpenRemovalDialog(league, confirmDelete: false);
    }

    [RelayCommand]
    private async Task ArchivePendingAsync()
    {
        if (PendingLeague is not { } league)
            return;

        await leagues.ArchiveLeagueAsync(league.LeagueId);
        session.ClearIfCurrentLeague(league.LeagueId);
        CloseRemovalDialog();
        StatusMessage = $"Archived \"{league.Name}\". Restore it from Archived leagues if you need it back.";
        await ReloadListsAsync();
    }

    [RelayCommand]
    private async Task RestoreLeagueAsync(LeagueSummary? summary)
    {
        if (summary is null)
            return;

        await leagues.RestoreLeagueAsync(summary.LeagueId);
        StatusMessage = $"Restored \"{summary.Name}\".";
        await ReloadListsAsync();
    }

    [RelayCommand]
    private async Task ConfirmPermanentDeleteAsync()
    {
        if (PendingLeague is not { } league || !NameMatches(league, ConfirmName))
            return;

        await leagues.DeleteLeaguePermanentlyAsync(league.LeagueId);
        session.ClearIfCurrentLeague(league.LeagueId);
        CloseRemovalDialog();
        StatusMessage = $"Permanently deleted \"{league.Name}\". A local backup was created first.";
        await ReloadListsAsync();
    }

    partial void OnConfirmNameChanged(string value)
    {
        CanPermanentlyDelete = PendingLeague is { } league && NameMatches(league, value);
    }

    private async Task ReloadListsAsync()
    {
        Leagues.Clear();
        foreach (var league in await leagues.ListLeaguesAsync())
            Leagues.Add(league);

        ArchivedLeagues.Clear();
        foreach (var league in await leagues.ListArchivedLeaguesAsync())
            ArchivedLeagues.Add(league);

        HasArchived = ArchivedLeagues.Count > 0;
    }

    private void OpenRemovalDialog(LeagueSummary summary, bool confirmDelete)
    {
        PendingLeague = summary;
        ConfirmName = "";
        CanPermanentlyDelete = false;
        HasRemovalDialog = true;
        IsConfirmingDelete = confirmDelete;
        if (confirmDelete)
        {
            RemovalTitle = $"Permanently delete \"{summary.Name}\"?";
            RemovalWarning = BuildPermanentDeleteWarning(summary);
        }
        else
        {
            RemovalTitle = $"Remove \"{summary.Name}\"?";
            RemovalWarning = BuildArchiveOrRemoveWarning(summary);
        }
    }

    private void CloseRemovalDialog()
    {
        HasRemovalDialog = false;
        IsConfirmingDelete = false;
        CanPermanentlyDelete = false;
        ConfirmName = "";
        PendingLeague = null;
        RemovalTitle = "";
        RemovalWarning = "";
    }

    private static string BuildArchiveOrRemoveWarning(LeagueSummary league)
    {
        return $"{DraftSummary(league)} Archive hides it and keeps every pick. Permanently delete erases the league, teams, drafts, and history.";
    }

    private static string BuildPermanentDeleteWarning(LeagueSummary league)
    {
        return $"{DraftSummary(league)} This cannot be undone in the app. Type the league name below to confirm. A local backup is created first.";
    }

    private static string DraftSummary(LeagueSummary league)
    {
        var drafts = league.DraftCount == 1 ? "1 draft" : $"{league.DraftCount} drafts";
        var status = league.ActiveDraftStatus is { } draftStatus
            ? $" Latest draft status: {draftStatus}."
            : "";
        return $"This league has {drafts}.{status}";
    }

    private static bool NameMatches(LeagueSummary league, string typed) =>
        !string.IsNullOrWhiteSpace(typed)
        && string.Equals(typed.Trim(), league.Name.Trim(), StringComparison.OrdinalIgnoreCase);
}
