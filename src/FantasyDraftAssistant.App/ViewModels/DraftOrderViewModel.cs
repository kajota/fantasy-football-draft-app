using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class DraftSeatRow : ObservableObject
{
    public required TeamId TeamId { get; init; }
    public required string TeamName { get; init; }
    public string TeamKey => TeamId.ToString();
    public string? OwnerName { get; init; }
    [ObservableProperty] private int _draftPosition;
    [ObservableProperty] private bool _canMoveUp;
    [ObservableProperty] private bool _canMoveDown;
}

public partial class DraftOrderViewModel(
    ILeagueService leagues,
    IDraftCommandService commands,
    SessionState session) : PageViewModel
{
    public ObservableCollection<DraftSeatRow> Seats { get; } = [];
    public ObservableCollection<string> Preview { get; } = [];
    public string[] Formats { get; } = ["Snake", "Linear", "Custom"];

    [ObservableProperty] private string _selectedFormat = "Snake";
    [ObservableProperty] private string _formatHelp = "";
    [ObservableProperty] private bool _hasLeague;
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private bool _canStart;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Draft Order";
        await ReloadAsync();
    }

    [RelayCommand]
    private void MoveUp(DraftSeatRow? row) => MoveSeat(row, -1);

    [RelayCommand]
    private void MoveDown(DraftSeatRow? row) => MoveSeat(row, 1);

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryBuildRequest(out var request) || request is null)
            return;

        try
        {
            await leagues.SaveDraftOrderAsync(request);
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        StatusMessage = "Saved first-round order and regenerated later rounds from the selected format.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task CreateDraftAsync()
    {
        if (session.LeagueId is not { } leagueId)
        {
            StatusMessage = "Select a league first.";
            return;
        }

        if (CanEdit && !await PersistCurrentOrderAsync())
            return;

        try
        {
            var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
            {
                LeagueId = leagueId,
                Name = $"{session.LeagueName ?? "League"} Draft"
            });
            session.DraftId = draft.DraftId;
            session.DraftName = draft.Name;
            session.BranchId = draft.ActiveBranchId;
            StatusMessage = "Draft created. Review the order, then start when keepers are set.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task StartAsync()
    {
        if (session.DraftId is not { } id)
            return;

        if (CanEdit && !await PersistCurrentOrderAsync())
            return;

        var result = await commands.StartDraftAsync(new StartDraftCommand(id));
        StatusMessage = result.Succeeded ? "Draft started. Open the Draft Room to pick." : result.Error;
        await ReloadAsync();
    }

    partial void OnSelectedFormatChanged(string value)
    {
        UpdateFormatHelp();
        RebuildPreview();
    }

    private async Task ReloadAsync()
    {
        Seats.Clear();
        Preview.Clear();
        HasLeague = session.LeagueId is not null;
        HasDraft = false;
        CanEdit = false;
        CanStart = false;
        UpdateFormatHelp();

        if (session.LeagueId is not { } leagueId)
        {
            StatusMessage ??= "Select or create a league first.";
            return;
        }

        var league = await leagues.GetLeagueAsync(leagueId);
        if (league is null)
        {
            StatusMessage = "Select or create a league first.";
            return;
        }

        SelectedFormat = FormatName(league.DraftType);
        var teams = (await leagues.GetTeamsAsync(leagueId)).OrderBy(t => t.DraftPosition).ToList();
        foreach (var team in teams)
        {
            Seats.Add(new DraftSeatRow
            {
                TeamId = team.TeamId,
                TeamName = team.Label,
                OwnerName = team.OwnerName,
                DraftPosition = team.DraftPosition
            });
        }

        if (session.DraftId is { } draftId)
        {
            var draft = await leagues.GetDraftAsync(draftId);
            if (draft is not null && draft.LeagueId.Equals(leagueId))
            {
                HasDraft = true;
                CanStart = draft.Status == DraftStatus.NotStarted;
                CanEdit = draft.Status == DraftStatus.NotStarted;
                if (draft.Status != DraftStatus.NotStarted)
                    StatusMessage ??= $"Draft is {draft.Status.ToString().ToLowerInvariant()}. The order is locked.";
            }
        }

        if (!HasDraft)
        {
            CanEdit = true;
            StatusMessage ??= "Set first-round seats, then create the draft. Later rounds follow Snake or Linear.";
        }

        RefreshSeatMoves();
        RebuildPreview();
    }

    private async Task<bool> PersistCurrentOrderAsync()
    {
        if (!TryBuildRequest(out var request) || request is null)
            return false;

        try
        {
            await leagues.SaveDraftOrderAsync(request);
            return true;
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            return false;
        }
    }

    private bool TryBuildRequest(out SaveDraftOrderRequest? request)
    {
        request = null;
        if (session.LeagueId is not { } leagueId)
        {
            StatusMessage = "Select a league first.";
            return false;
        }

        if (Seats.Count == 0)
        {
            StatusMessage = "This league has no teams.";
            return false;
        }

        request = new SaveDraftOrderRequest
        {
            LeagueId = leagueId,
            DraftId = HasDraft ? session.DraftId : null,
            DraftType = ParseFormat(SelectedFormat),
            Teams = Seats.Select((seat, index) => new TeamDraftPosition
            {
                TeamId = seat.TeamId,
                Name = seat.TeamName,
                OwnerName = seat.OwnerName,
                DraftPosition = index + 1
            }).ToList()
        };
        return true;
    }

    private void MoveSeat(DraftSeatRow? row, int delta)
    {
        if (row is null || !CanEdit)
            return;

        var index = Seats.IndexOf(row);
        var target = index + delta;
        if (index < 0 || target < 0 || target >= Seats.Count)
            return;

        Seats.Move(index, target);
        RefreshSeatMoves();
        RebuildPreview();
        StatusMessage = $"Moved {row.TeamName} to seat {target + 1}. Save to keep it.";
    }

    private void RefreshSeatMoves()
    {
        for (var i = 0; i < Seats.Count; i++)
        {
            Seats[i].DraftPosition = i + 1;
            Seats[i].CanMoveUp = CanEdit && i > 0;
            Seats[i].CanMoveDown = CanEdit && i < Seats.Count - 1;
        }
    }

    private void RebuildPreview()
    {
        Preview.Clear();
        if (Seats.Count == 0)
            return;

        var teams = Seats.Select((seat, index) => new TeamDraftPosition
        {
            TeamId = seat.TeamId,
            Name = seat.TeamName,
            OwnerName = seat.OwnerName,
            DraftPosition = index + 1
        }).ToList();

        var rounds = 3;
        try
        {
            foreach (var slot in DraftSlotGenerator.Generate(ParseFormat(SelectedFormat), teams, rounds))
            {
                var team = Seats.First(s => s.TeamId.Equals(slot.TeamId));
                Preview.Add($"{slot.Round}.{slot.RoundPick:00}  {team.TeamName}");
            }

            Preview.Add("… remaining rounds follow the same pattern.");
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Preview.Add(ex.Message);
        }
    }

    private void UpdateFormatHelp()
    {
        FormatHelp = ParseFormat(SelectedFormat) switch
        {
            DraftType.Snake => "Snake: round 1 is the seat order below. Even rounds reverse it.",
            DraftType.Linear => "Linear: every round uses the same seat order.",
            _ => "Custom: same as linear for now. Per-pick trades come later."
        };
    }

    private static DraftType ParseFormat(string name) =>
        Enum.TryParse<DraftType>(name, out var type) ? type : DraftType.Snake;

    private static string FormatName(DraftType type) => type.ToString();
}
