using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class AiAnalystPanel : ObservableObject
{
    public required string ProviderKey { get; init; }
    public required string Title { get; init; }
    public bool Enabled { get; init; }
    public string? Model { get; init; }
    public decimal? SpendLimit { get; init; }
    [ObservableProperty] private string _status = "Idle";
    [ObservableProperty] private string _response = "";
}

public sealed class BoardRow
{
    public required string RoundPick { get; init; }
    public required string Team { get; init; }
    public required string Player { get; init; }
    public required string Position { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsMine { get; init; }
    public string Marker => IsCurrent ? "▶" : " ";
}

public partial class PlayerRow : ObservableObject
{
    public required PlayerId PlayerId { get; init; }
    [ObservableProperty] private int? _rank;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _position = "";
    [ObservableProperty] private string _nflTeam = "";
    [ObservableProperty] private string? _adp;
    [ObservableProperty] private decimal? _proj;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private int? _tier;
}

public partial class DraftRoomViewModel : PageViewModel
{
    private readonly IDraftCommandService _commands;
    private readonly IDraftStateService _drafts;
    private readonly IDraftQueryService _queries;
    private readonly IAnalyticsService _analytics;
    private readonly IAiProviderRegistry _ai;
    private readonly IAiConfigStore _aiConfigs;
    private readonly IAiUsageService _usage;
    private readonly IDraftChangeNotifier _notifier;
    private readonly SessionState _session;

    public DraftRoomViewModel(
        IDraftCommandService commands,
        IDraftStateService drafts,
        IDraftQueryService queries,
        IAnalyticsService analytics,
        IAiProviderRegistry ai,
        IAiConfigStore aiConfigs,
        IAiUsageService usage,
        IDraftChangeNotifier notifier,
        SessionState session)
    {
        _commands = commands;
        _drafts = drafts;
        _queries = queries;
        _analytics = analytics;
        _ai = ai;
        _aiConfigs = aiConfigs;
        _usage = usage;
        _notifier = notifier;
        _session = session;
        _notifier.DraftChanged += OnDraftChanged;
    }

    public ObservableCollection<BoardRow> Board { get; } = [];
    public ObservableCollection<PlayerRow> Available { get; } = [];
    public ObservableCollection<PlayerRow> Queue { get; } = [];
    public ObservableCollection<string> Roster { get; } = [];
    public ObservableCollection<string> Alerts { get; } = [];
    public ObservableCollection<AiAnalystPanel> Analysts { get; } = [];

    [ObservableProperty] private string _leagueName = "No draft open";
    [ObservableProperty] private string _locationLine = "Open or start a draft to see the current pick.";
    [ObservableProperty] private string _roundPick = "—";
    [ObservableProperty] private string _selectingTeam = "—";
    [ObservableProperty] private string _userNext = "—";
    [ObservableProperty] private string _sourceMode = "Manual";
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _queueEmpty = true;
    [ObservableProperty] private bool _rosterEmpty = true;
    [ObservableProperty] private bool _hasAlerts;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _positionFilter = "All";
    [ObservableProperty] private PlayerRow? _selectedPlayer;
    [ObservableProperty] private string _aiPrompt = "Who should I take here?";
    [ObservableProperty] private int _stateVersion;
    [ObservableProperty] private bool _canRedo;

    public string[] Positions { get; } = ["All", "QB", "RB", "WR", "TE", "K", "DEF"];

    public override async Task OnNavigatedToAsync()
    {
        Title = "Draft Room";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ReloadAsync()
    {
        if (_session.DraftId is not { } draftId)
        {
            HasDraft = false;
            LeagueName = "No draft open";
            LocationLine = "Create a draft from Draft Order, or start a mock draft from Leagues.";
            StatusMessage = "Start or open a draft first.";
            return;
        }

        var state = await _drafts.GetWorkingStateAsync(draftId, _session.BranchId);
        if (state is null)
        {
            StatusMessage = "Draft not found.";
            return;
        }

        HasDraft = true;
        _session.BranchId = state.ActiveBranch.BranchId;
        StateVersion = state.Draft.CurrentStateVersion;
        SourceMode = state.Draft.SourceMode.ToString();
        CanRedo = state.Redo is not null;
        var snapshot = await _analytics.GetSnapshotAsync(draftId, state.ActiveBranch.BranchId);
        LeagueName = state.League.Name;
        RoundPick = snapshot.CurrentRoundPick;
        var team = snapshot.CurrentTeamId is { } teamId
            ? state.Teams.FirstOrDefault(t => t.TeamId.Equals(teamId))?.Label
            : null;
        SelectingTeam = team ?? "—";
        var currentSlot = state.CurrentSlot;
        LocationLine = currentSlot is null
            ? $"{state.League.Name} · draft complete · {state.ActiveSelections.Count} picks"
            : $"{state.League.Name} · Round {currentSlot.Round} of {state.League.RoundCount} · Pick {snapshot.CurrentRoundPick} (overall {currentSlot.OverallPick}) · {team} is on the clock";
        UserNext = snapshot.UserNextRoundPick is null
            ? "You have no remaining pick"
            : snapshot.PicksUntilUser == 0
                ? "This is your pick"
                : $"Your next pick is {snapshot.UserNextRoundPick} · {snapshot.PicksUntilUser} pick(s) away";

        Board.Clear();
        var context = new QueryContext { DraftId = draftId, BranchId = state.ActiveBranch.BranchId };
        var playersById = (await _drafts.GetPlayersAsync()).ToDictionary(p => p.PlayerId);
        var userTeam = state.League.UserTeamId;
        foreach (var slot in state.Slots.OrderBy(s => s.OverallPick))
        {
            state.ActiveSelections.TryGetValue(slot.OverallPick, out var selection);
            var slotTeam = state.Teams.FirstOrDefault(t => t.TeamId.Equals(slot.TeamId));
            var isCurrent = currentSlot is not null && slot.OverallPick == currentSlot.OverallPick;
            string playerName;
            string position;
            if (selection is not null)
            {
                playersById.TryGetValue(selection.PlayerId, out var pickPlayer);
                playerName = pickPlayer?.Name ?? selection.PlayerId.ToString();
                position = pickPlayer?.PrimaryPosition.ToString() ?? "";
            }
            else if (isCurrent)
            {
                playerName = "On the clock";
                position = "";
            }
            else
            {
                playerName = "";
                position = "";
            }

            Board.Add(new BoardRow
            {
                RoundPick = $"{slot.Round}.{slot.RoundPick:00}",
                Team = slotTeam?.Label ?? "",
                Player = playerName,
                Position = position,
                IsCurrent = isCurrent,
                IsMine = userTeam is { } mine && slot.TeamId.Equals(mine)
            });
        }

        var available = await _queries.GetAvailablePlayersAsync(context, new PlayerFilter
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search,
            Position = Enum.TryParse<PlayerPosition>(PositionFilter, out var pos) ? pos : null,
            MaxResults = 80
        });
        Available.Clear();
        foreach (var player in available.Players)
        {
            Available.Add(new PlayerRow
            {
                PlayerId = PlayerId.Parse(player.PlayerId),
                Rank = player.OverallRank,
                Name = player.Name,
                Position = player.Position,
                NflTeam = player.NflTeam,
                Adp = player.AdpRoundPick,
                Proj = player.ProjectedPoints,
                Status = player.Status,
                Tier = player.Tier
            });
        }

        if (SelectedPlayer is null || Available.All(p => !p.PlayerId.Equals(SelectedPlayer.PlayerId)))
            SelectedPlayer = Available.FirstOrDefault();

        Queue.Clear();
        var queue = await _queries.GetMyQueueAsync(context);
        foreach (var player in queue.Players)
        {
            Queue.Add(new PlayerRow
            {
                PlayerId = PlayerId.Parse(player.PlayerId),
                Name = player.Name,
                Position = player.Position,
                NflTeam = player.NflTeam,
                Adp = player.AdpRoundPick
            });
        }

        Roster.Clear();
        if (state.League.UserTeamId is { } user)
        {
            var roster = await _queries.GetTeamRosterAsync(context, user);
            foreach (var player in roster.Players)
                Roster.Add($"{player.RoundPick}  {player.Position}  {player.Name}");
        }

        Alerts.Clear();
        foreach (var alert in snapshot.Alerts)
            Alerts.Add(alert.Message);

        QueueEmpty = Queue.Count == 0;
        RosterEmpty = Roster.Count == 0;
        HasAlerts = Alerts.Count > 0;
        await RefreshAnalystsAsync();
    }

    [RelayCommand]
    private async Task DraftSelectedAsync()
    {
        if (SelectedPlayer is null || _session.DraftId is not { } draftId)
            return;
        var result = await _commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, SelectedPlayer.PlayerId));
        StatusMessage = result.Succeeded ? $"Drafted {SelectedPlayer.Name}." : result.Error;
        if (result.Succeeded)
            Search = "";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task DraftQueueAsync()
    {
        if (Queue.Count == 0 || _session.DraftId is not { } draftId)
            return;
        var top = Queue[0];
        var result = await _commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, top.PlayerId));
        StatusMessage = result.Succeeded ? $"Drafted queued {top.Name}." : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task QueueSelectedAsync()
    {
        if (SelectedPlayer is null || _session.DraftId is not { } draftId)
            return;
        await _drafts.AddToQueueAsync(new QueueAddCommand(draftId, SelectedPlayer.PlayerId));
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task RollbackLastAsync()
    {
        if (_session.DraftId is not { } draftId)
            return;
        var state = await _drafts.GetWorkingStateAsync(draftId);
        if (state is null || state.ActiveSelections.Count == 0)
            return;
        var last = state.ActiveSelections.Keys.Max();
        var result = await _commands.RollbackAsync(new RollbackDraftCommand(draftId, last - 1));
        StatusMessage = result.Succeeded ? $"Rolled back pick {last}." : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task RedoAsync()
    {
        if (_session.DraftId is not { } draftId)
            return;
        var result = await _commands.RedoAsync(new RedoDraftCommand(draftId));
        StatusMessage = result.Succeeded ? "Redo applied." : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task AskAiAsync()
    {
        if (_session.DraftId is not { } draftId || _session.BranchId is not { } branchId)
            return;

        await RefreshAnalystsAsync();
        var enabled = Analysts.Where(a => a.Enabled).ToList();
        if (enabled.Count == 0)
        {
            StatusMessage = "No AI provider is enabled. Configure ChatGPT, Claude, or Grok under AI Providers.";
            return;
        }

        var version = StateVersion;
        var prompt = string.IsNullOrWhiteSpace(AiPrompt) ? "Who should I take here?" : AiPrompt;
        var tasks = enabled.Select(panel => AskOneAsync(panel, draftId, branchId, version, prompt));
        await Task.WhenAll(tasks);
    }

    private async Task AskOneAsync(AiAnalystPanel panel, DraftId draftId, BranchId branchId, int version, string prompt)
    {
        var adapter = _ai.Get(panel.ProviderKey);
        if (adapter is null)
        {
            panel.Status = "Missing adapter";
            panel.Response = "This provider is not registered.";
            return;
        }

        if (panel.SpendLimit is { } limit)
        {
            var spent = await _usage.EstimatedDraftSpendAsync(draftId, panel.ProviderKey);
            if (spent >= limit)
            {
                panel.Status = "Spend limit reached";
                panel.Response = "This provider hit its per-draft cap. Draft recording continues.";
                return;
            }
        }

        panel.Status = "Generating";
        panel.Response = "";
        try
        {
            await foreach (var chunk in adapter.StreamAnalysisAsync(new AiAnalysisRequest
            {
                DraftId = draftId,
                BranchId = branchId,
                StateVersion = version,
                Prompt = prompt,
                FastMode = true,
                Model = panel.Model
            }))
            {
                if (chunk.Error is not null)
                {
                    panel.Status = "Failed";
                    panel.Response = chunk.Error;
                    return;
                }

                if (!string.IsNullOrEmpty(chunk.Text))
                    panel.Response += chunk.Text;
                if (chunk.IsComplete)
                    panel.Status = StateVersion == version ? "Ready" : $"Stale (v{version} / current {StateVersion})";
            }
        }
        catch (Exception ex)
        {
            panel.Status = "Failed";
            panel.Response = ex.Message;
        }
    }

    private async Task RefreshAnalystsAsync()
    {
        var configs = await _aiConfigs.ListAsync();
        var existing = Analysts.ToDictionary(a => a.ProviderKey);
        Analysts.Clear();
        foreach (var descriptor in Core.Ai.AiProviderCatalog.All)
        {
            var config = configs.FirstOrDefault(c => c.ProviderKey == descriptor.ProviderKey);
            existing.TryGetValue(descriptor.ProviderKey, out var prior);
            Analysts.Add(new AiAnalystPanel
            {
                ProviderKey = descriptor.ProviderKey,
                Title = descriptor.ProductName,
                Enabled = config?.Enabled == true,
                Model = config?.Model ?? descriptor.DefaultModel,
                SpendLimit = config?.PerDraftSpendLimit,
                Status = config?.Enabled == true ? prior?.Status ?? "Idle" : "Disabled",
                Response = prior?.Response ?? (config?.Enabled == true
                    ? "Ask when you are on the clock."
                    : "Enable this provider under AI Providers.")
            });
        }
    }

    partial void OnSearchChanged(string value) => _ = ReloadAsync();
    partial void OnPositionFilterChanged(string value) => _ = ReloadAsync();

    private void OnDraftChanged(object? sender, DraftChangedEventArgs e)
    {
        if (_session.DraftId is { } id && e.DraftId.Equals(id))
            _ = ReloadAsync();
    }
}
