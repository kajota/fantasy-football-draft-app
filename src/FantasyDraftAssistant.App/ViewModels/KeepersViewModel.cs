using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class KeeperTeamRow : ObservableObject
{
    private IReadOnlyList<DraftSlot> _slots = [];

    public required TeamId TeamId { get; init; }
    public required string TeamName { get; init; }
    public string TeamKey => TeamId.ToString();
    public string? OwnerName { get; init; }

    [ObservableProperty] private PlayerId? _playerId;
    [ObservableProperty] private string _playerName = "No keeper";
    [ObservableProperty] private string _playerDetail = "";
    [ObservableProperty] private int _roundCost = 1;
    [ObservableProperty] private string _roundCostText = "1";
    [ObservableProperty] private bool _hasInvalidRound;
    [ObservableProperty] private string? _notes;
    [ObservableProperty] private string _slotLabel = "—";
    [ObservableProperty] private bool _hasKeeper;

    public void BindSlots(IReadOnlyList<DraftSlot> slots)
    {
        _slots = slots;
        RefreshSlot();
    }

    public void SetPlayer(Player player)
    {
        PlayerId = player.PlayerId;
        PlayerName = player.Name;
        PlayerDetail = $"{player.PrimaryPosition} · {player.NflTeam}";
        HasKeeper = true;
        RefreshSlot();
    }

    public void ClearPlayer()
    {
        PlayerId = null;
        PlayerName = "No keeper";
        PlayerDetail = "";
        Notes = null;
        HasKeeper = false;
        RefreshSlot();
    }

    partial void OnRoundCostChanged(int value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        if (RoundCostText != text)
            RoundCostText = text;
        RefreshSlot();
    }

    partial void OnRoundCostTextChanged(string value)
    {
        if (int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var round)
            && round >= 1)
        {
            HasInvalidRound = false;
            if (RoundCost != round)
                RoundCost = round;
            else
                RefreshSlot();
            return;
        }

        HasInvalidRound = true;
        RefreshSlot();
    }

    private void RefreshSlot()
    {
        if (PlayerId is not { } playerId)
        {
            SlotLabel = "—";
            return;
        }

        if (HasInvalidRound)
        {
            SlotLabel = "Whole number only";
            return;
        }

        var slot = KeeperRules.ResolveSlot(_slots, new Keeper
        {
            KeeperId = KeeperId.New(),
            DraftId = DraftId.New(),
            TeamId = TeamId,
            PlayerId = playerId,
            RoundCost = RoundCost
        });
        SlotLabel = slot is null ? "No slot in that round" : $"{slot.Round}.{slot.RoundPick:00}";
    }
}

public sealed class KeeperPlayerChoice
{
    public required PlayerId PlayerId { get; init; }
    public required string Name { get; init; }
    public required string Detail { get; init; }
    public string? TakenBy { get; init; }
    public bool IsAvailable => TakenBy is null;
}

public partial class KeepersViewModel(
    ILeagueService leagues,
    IDraftStateService drafts,
    IFantasyDataProviderRegistry fantasyData,
    SessionState session) : PageViewModel
{
    private IReadOnlyList<Player> _players = [];
    private IReadOnlyList<DraftSlot> _slots = [];

    public ObservableCollection<KeeperTeamRow> Teams { get; } = [];
    public ObservableCollection<KeeperPlayerChoice> PlayerChoices { get; } = [];

    [ObservableProperty] private bool _hasLeague;
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _showCreateDraft;
    [ObservableProperty] private bool _hasPlayers;
    [ObservableProperty] private bool _canEdit;
    [ObservableProperty] private bool _showLocked;
    [ObservableProperty] private bool _showReopened;
    [ObservableProperty] private bool _showPicker;
    [ObservableProperty] private KeeperPlayerChoice? _selectedChoice;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _pickerTitle = "Assign keeper";
    [ObservableProperty] private string _summary = "";
    [ObservableProperty] private KeeperTeamRow? _assigningTeam;

    public override async Task OnNavigatedToAsync()
    {
        Title = "Keepers";
        ClosePicker();
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
            StatusMessage = "Draft created. Assign keepers, then start the draft from Draft Order.";
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task LoadPlayersAsync()
    {
        StatusMessage = "Loading player data...";
        var result = await fantasyData.RefreshPreferredAsync(new FantasyDataRefreshRequest(), CancellationToken.None);
        StatusMessage = result.Succeeded
            ? result.Error ?? $"Cached {result.PlayersWritten} players."
            : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task BeginAssignAsync(KeeperTeamRow? row)
    {
        if (row is null || !CanEdit)
            return;

        if (!HasPlayers)
        {
            await LoadPlayersAsync();
            if (!HasPlayers)
                return;
        }

        AssigningTeam = row;
        PickerTitle = $"Assign keeper for {row.TeamName}";
        Search = "";
        SelectedChoice = null;
        ShowPicker = true;
        RefreshChoices();
    }

    [RelayCommand]
    private void CancelAssign() => ClosePicker();

    [RelayCommand]
    private void PickPlayer(KeeperPlayerChoice? choice)
    {
        if (choice is null || AssigningTeam is null || !CanEdit)
            return;
        if (!choice.IsAvailable)
        {
            StatusMessage = $"{choice.Name} is already kept by {choice.TakenBy}.";
            return;
        }

        var player = _players.FirstOrDefault(p => p.PlayerId.Equals(choice.PlayerId));
        if (player is null)
            return;

        var teamName = AssigningTeam.TeamName;
        AssigningTeam.SetPlayer(player);
        ClosePicker();
        RefreshSummary();
        StatusMessage = $"{player.Name} assigned to {teamName}. Click Save keepers to store it.";
    }

    [RelayCommand]
    private void ConfirmPick() => PickPlayer(SelectedChoice);

    [RelayCommand]
    private void ClearKeeper(KeeperTeamRow? row)
    {
        if (row is null || !CanEdit)
            return;
        var name = row.HasKeeper ? row.PlayerName : null;
        row.ClearPlayer();
        RefreshSummary();
        StatusMessage = name is null ? "No keeper on that team." : $"Cleared {name}. Save to keep the change.";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (session.DraftId is not { } draftId || !CanEdit)
            return;

        var specs = new List<KeeperSpec>();
        foreach (var row in Teams)
        {
            if (row.PlayerId is not { } playerId)
                continue;
            if (row.HasInvalidRound)
            {
                StatusMessage = $"{row.TeamName}: round must be a whole number, for example 4, not 4.3.";
                return;
            }
            if (row.RoundCost < 1)
            {
                StatusMessage = $"{row.TeamName}: keeper round must be at least 1.";
                return;
            }

            if (row.SlotLabel.StartsWith("No slot", StringComparison.Ordinal))
            {
                StatusMessage = $"{row.TeamName}: there is no draft slot in round {row.RoundCost}.";
                return;
            }

            specs.Add(new KeeperSpec
            {
                TeamId = row.TeamId,
                PlayerId = playerId,
                RoundCost = row.RoundCost,
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim()
            });
        }

        var validation = KeeperRules.Validate(specs);
        if (!validation.IsValid)
        {
            StatusMessage = validation.Error;
            return;
        }

        var working = await drafts.GetWorkingStateAsync(draftId);
        var alreadyStarted = working?.Draft.Status == DraftStatus.InProgress;

        try
        {
            await leagues.SaveKeepersAsync(new SaveKeepersRequest
            {
                DraftId = draftId,
                Keepers = specs
            });
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
            await ReloadAsync();
            return;
        }

        var savedMessage = specs.Count == 0
            ? alreadyStarted
                ? "Saved. No keepers on the board."
                : "Saved. No keepers assigned."
            : alreadyStarted
                ? $"Saved {specs.Count} keeper(s). They are on the board now."
                : $"Saved {specs.Count} keeper(s). They lock in once regular picks are made.";
        await ReloadAsync();
        StatusMessage = savedMessage;
    }

    partial void OnSearchChanged(string value)
    {
        if (ShowPicker)
            RefreshChoices();
    }

    private async Task ReloadAsync()
    {
        Teams.Clear();
        HasLeague = session.LeagueId is not null;
        HasDraft = false;
        ShowCreateDraft = false;
        ShowLocked = false;
        ShowReopened = false;
        HasPlayers = false;
        CanEdit = false;
        Summary = "";
        _slots = [];
        _players = [];

        if (session.LeagueId is not { } leagueId)
        {
            StatusMessage = "Select or create a league first.";
            return;
        }

        var listed = await leagues.ListDraftsAsync(leagueId);
        var current = session.DraftId is { } sessionDraft
            ? listed.FirstOrDefault(item => item.DraftId.Equals(sessionDraft))
            : null;
        if (current is null)
        {
            current = listed.FirstOrDefault(item => item.Status == DraftStatus.NotStarted);
        }
        else if (current.Status != DraftStatus.NotStarted)
        {
            var peek = await drafts.GetWorkingStateAsync(current.DraftId);
            if (peek is null || !KeeperRules.CanEditKeepers(current.Status, peek.ActiveSelections.Values))
            {
                var open = listed.FirstOrDefault(item => item.Status == DraftStatus.NotStarted);
                if (open is not null)
                {
                    session.DraftId = open.DraftId;
                    session.DraftName = open.Name;
                    session.BranchId = open.ActiveBranchId;
                    current = open;
                }
            }
        }

        if (current is null)
        {
            ShowCreateDraft = true;
            StatusMessage = "This league has no draft yet. Create one, assign keepers, then start it from Draft Order.";
            return;
        }

        var draftId = current.DraftId;
        session.DraftId = draftId;
        session.DraftName = current.Name;
        session.BranchId = current.ActiveBranchId;

        HasDraft = true;
        var state = await drafts.GetWorkingStateAsync(draftId);
        _slots = state?.Slots ?? [];
        _players = await drafts.GetPlayersAsync();
        HasPlayers = _players.Count > 0;
        var started = current.Status != DraftStatus.NotStarted;
        var canEdit = state is not null && KeeperRules.CanEditKeepers(current.Status, state.ActiveSelections.Values);
        ShowLocked = started && !canEdit;
        ShowReopened = started && canEdit;
        CanEdit = canEdit;

        var saved = (await leagues.GetKeepersAsync(draftId))
            .GroupBy(keeper => keeper.TeamId)
            .ToDictionary(group => group.Key, group => group.First());
        var teams = state?.Teams ?? await leagues.GetTeamsAsync(leagueId);
        foreach (var team in teams.OrderBy(t => t.DraftPosition))
        {
            var row = new KeeperTeamRow
            {
                TeamId = team.TeamId,
                TeamName = team.Label,
                OwnerName = team.OwnerName,
                RoundCost = 1
            };
            row.BindSlots(_slots);
            if (saved.TryGetValue(team.TeamId, out var keeper))
            {
                var player = _players.FirstOrDefault(p => p.PlayerId.Equals(keeper.PlayerId));
                row.PlayerId = keeper.PlayerId;
                row.PlayerName = player?.Name ?? keeper.PlayerId.ToString();
                row.PlayerDetail = player is null
                    ? "Player not in the local cache"
                    : $"{player.PrimaryPosition} · {player.NflTeam}";
                row.RoundCost = keeper.RoundCost;
                row.Notes = keeper.Notes;
                row.HasKeeper = true;
                row.BindSlots(_slots);
            }

            Teams.Add(row);
        }

        RefreshSummary();
        if (ShowLocked)
            StatusMessage = "Keepers are locked because this draft has regular picks. Undo every non-keeper pick in the Draft Room to change them, or create a new draft.";
        else if (ShowReopened)
            StatusMessage = "This draft already started, but there are no regular picks yet. Assign keepers and Save to put them on the board.";
        else if (!HasPlayers)
            StatusMessage = "Player cache is empty. Click Assign and the app will load Sleeper/seed players, or use Load players.";
        else
            StatusMessage = "Click Assign on a team, pick a player, then Save keepers. They take that team's pick in the chosen round.";
    }

    private void RefreshChoices()
    {
        PlayerChoices.Clear();
        if (!ShowPicker)
            return;

        var taken = new Dictionary<PlayerId, string>();
        foreach (var row in Teams)
        {
            if (row.PlayerId is not { } id)
                continue;
            if (AssigningTeam is { } current && row.TeamId.Equals(current.TeamId))
                continue;
            taken[id] = row.TeamName;
        }

        var query = Search.Trim();
        IEnumerable<Player> source = _players;
        if (!string.IsNullOrWhiteSpace(query))
        {
            source = _players.Where(p =>
                p.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.NflTeam.Contains(query, StringComparison.OrdinalIgnoreCase)
                || p.PrimaryPosition.ToString().Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        foreach (var player in source.OrderBy(p => p.Name).Take(40))
        {
            taken.TryGetValue(player.PlayerId, out var owner);
            PlayerChoices.Add(new KeeperPlayerChoice
            {
                PlayerId = player.PlayerId,
                Name = player.Name,
                Detail = $"{player.PrimaryPosition} · {player.NflTeam}",
                TakenBy = owner
            });
        }
    }

    private void RefreshSummary()
    {
        var assigned = Teams.Count(t => t.HasKeeper);
        Summary = Teams.Count == 0
            ? ""
            : $"{assigned} of {Teams.Count} teams have a keeper.";
    }

    private void ClosePicker()
    {
        ShowPicker = false;
        AssigningTeam = null;
        SelectedChoice = null;
        Search = "";
        PlayerChoices.Clear();
    }
}
