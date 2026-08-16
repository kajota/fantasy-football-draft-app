using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.App.ViewModels;

public partial class AiAnalystPanel : ObservableObject
{
    public required string ProviderKey { get; init; }
    public required string Title { get; init; }
    public bool Enabled { get; init; }
    public string? Model { get; init; }
    public string Role { get; init; } = AiAnalysisMode.FastAdvisor;
    public decimal? SpendLimit { get; init; }
    [ObservableProperty] private bool _includeInAsk = true;
    [ObservableProperty] private string _status = "Idle";
    [ObservableProperty] private string _response = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Heading))]
    private string _spendLabel = "";

    public string Heading
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Model) ? Title : $"{Title} · {Model}";
            return string.IsNullOrWhiteSpace(SpendLabel) ? name : $"{name} ({SpendLabel})";
        }
    }
}

public sealed class AiConversationTurn
{
    public required string Provider { get; init; }
    public required string Prompt { get; init; }
    public required string Response { get; init; }
    public required string When { get; init; }
    public string Header => $"{When} · {Provider}";
}

public sealed class BoardRow
{
    public required string RoundPick { get; init; }
    public required string Team { get; init; }
    public required TeamId TeamId { get; init; }
    public required string Player { get; init; }
    public required string Position { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsMine { get; init; }
    public string TeamKey => TeamId.ToString();
    public string Marker => IsCurrent ? "▶" : " ";
}

public sealed class DraftBoardTeamHeader
{
    public required TeamId TeamId { get; init; }
    public required string Label { get; init; }
    public bool IsMine { get; init; }
    public string TeamKey => TeamId.ToString();
    public IBrush Fill => IsMine ? DraftBoardPalette.MineHeader : DraftBoardPalette.Header;
    public IBrush Foreground => IsMine ? DraftBoardPalette.MineHeaderText : DraftBoardPalette.HeaderText;
}

public sealed class DraftBoardRoundRow
{
    public required int Round { get; init; }
    public required IReadOnlyList<DraftBoardCell> Cells { get; init; }
}

public sealed class RosterSlotRow
{
    public required string SlotCode { get; init; }
    public required string DisplayName { get; init; }
    public required string Player { get; init; }
    public required string Detail { get; init; }
    public required string Position { get; init; }
    public bool IsFilled { get; init; }
    public bool IsNeed { get; init; }
    public bool ShowInOverview { get; init; }
    public IBrush Fill => DraftBoardPalette.Fill(Position, !IsFilled, IsNeed);
    public IBrush Foreground => IsFilled ? DraftBoardPalette.CellText : DraftBoardPalette.EmptyText;
}

public sealed class TauntTargetOption
{
    public required TeamId TeamId { get; init; }
    public required string Label { get; init; }
    public string TeamKey => TeamId.ToString();
    public override string ToString() => Label;
}

public sealed class DraftBoardCell
{
    public required string Player { get; init; }
    public required string Position { get; init; }
    public required string RoundPick { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsEmpty { get; init; }
    public bool IsMine { get; init; }
    public IBrush Fill => DraftBoardPalette.Fill(Position, IsEmpty, IsCurrent);
    public IBrush Border => IsCurrent ? DraftBoardPalette.CurrentBorder : DraftBoardPalette.CellBorder;
    public Thickness BorderThickness => IsCurrent ? new Thickness(2) : new Thickness(1);
    public IBrush Foreground => IsEmpty && !IsCurrent ? DraftBoardPalette.EmptyText : DraftBoardPalette.CellText;
    public string Tooltip => string.IsNullOrWhiteSpace(Player)
        ? RoundPick
        : string.IsNullOrWhiteSpace(Position) ? $"{RoundPick} · {Player}" : $"{RoundPick} · {Position} · {Player}";

    public static DraftBoardCell From(DraftGridCell cell) => new()
    {
        Player = cell.Player,
        Position = cell.Position,
        RoundPick = cell.RoundPick,
        IsCurrent = cell.IsCurrent,
        IsEmpty = cell.IsEmpty,
        IsMine = cell.IsMine
    };
}

public static class DraftBoardPalette
{
    public static readonly IBrush Header = Brush("#2E7D32");
    public static readonly IBrush HeaderText = Brush("#F4FFF4");
    public static readonly IBrush MineHeader = Brush("#E8A317");
    public static readonly IBrush MineHeaderText = Brush("#1A1204");
    public static readonly IBrush Round = Brush("#1B5E20");
    public static readonly IBrush CurrentBorder = Brush("#E8A317");
    public static readonly IBrush CellBorder = Brush("#0B1220");
    public static readonly IBrush CellText = Brush("#FFFFFF");
    public static readonly IBrush EmptyText = Brush("#8B97A8");
    public static readonly IBrush Empty = Brush("#1C2638");
    public static readonly IBrush CurrentEmpty = Brush("#3A3018");
    public static readonly IBrush Qb = Brush("#00ACC1");
    public static readonly IBrush Rb = Brush("#E53935");
    public static readonly IBrush Wr = Brush("#43A047");
    public static readonly IBrush Te = Brush("#1E88E5");
    public static readonly IBrush K = Brush("#8E24AA");
    public static readonly IBrush Def = Brush("#F9A825");
    public static readonly IBrush Other = Brush("#546E7A");

    public static IBrush Fill(string position, bool isEmpty, bool isCurrent)
    {
        if (isEmpty)
            return isCurrent ? CurrentEmpty : Empty;
        return position.ToUpperInvariant() switch
        {
            "QB" => Qb,
            "RB" => Rb,
            "WR" => Wr,
            "TE" => Te,
            "K" => K,
            "DEF" => Def,
            _ => Other
        };
    }

    private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
}

public partial class PlayerRow : ObservableObject
{
    public required PlayerId PlayerId { get; init; }
    public QueueItemId? QueueItemId { get; init; }
    [ObservableProperty] private int? _rank;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _position = "";
    [ObservableProperty] private string _nflTeam = "";
    [ObservableProperty] private string? _adp;
    [ObservableProperty] private decimal? _proj;
    [ObservableProperty] private string _projLabel = "—";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private string _statusLabel = "";
    [ObservableProperty] private string _injuryDetail = "";
    [ObservableProperty] private bool _isInjured;
    [ObservableProperty] private int? _tier;
    [ObservableProperty] private bool _canMoveUp;
    [ObservableProperty] private bool _canMoveDown;
    public string? FantasyProsUrl { get; init; }
    public string? SleeperUrl { get; init; }
    public string? YahooUrl { get; init; }
    public bool HasFantasyProsLink => FantasyProsUrl is not null;
    public bool HasSleeperLink => SleeperUrl is not null;
    public bool HasYahooLink => YahooUrl is not null;
    public bool HasExternalLinks => HasFantasyProsLink || HasSleeperLink || HasYahooLink;
    public Uri? FantasyProsUri => ToUri(FantasyProsUrl);
    public Uri? SleeperUri => ToUri(SleeperUrl);
    public Uri? YahooUri => ToUri(YahooUrl);
    public string? HandcuffFor { get; init; }
    public bool IsHandcuff => !string.IsNullOrWhiteSpace(HandcuffFor);
    public string HandcuffLabel => IsHandcuff ? $"Cuff · {HandcuffFor}" : "";
    public string HandcuffDetail => IsHandcuff
        ? $"Same NFL team as {HandcuffFor} on your roster. Typical handcuff: the backup if your starter misses time."
        : "";
    public IBrush HandcuffFill => IsHandcuff ? DraftBoardPalette.CurrentEmpty : Brushes.Transparent;

    private static Uri? ToUri(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri : null;
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
    private readonly IAiResponseStore _responses;
    private readonly IDraftChangeNotifier _notifier;
    private readonly IFantasyDataWriter _fantasyData;
    private readonly ITeamPortraitStore _portraits;
    private readonly IFileSavePicker _files;
    private readonly IMockDraftService _mock;
    private readonly SessionState _session;
    private bool _muteExternalReload;
    private bool _suppressSourceReload;
    private DraftId? _conversationDraft;
    private BranchId? _conversationBranch;
    private bool _suppressRosterTeamChange;
    private readonly HashSet<string> _seenWatchKeys = new(StringComparer.Ordinal);
    private bool _watchSeeded;
    private BranchId? _autoAskBranch;
    private int _lastAutoAskOverall = -1;
    private bool _boardReactionBusy;
    private bool _suppressBoardReaction;
    private AnalyticsSnapshot? _pendingBoardReaction;
    private bool _pauseMock;

    public DraftRoomViewModel(
        IDraftCommandService commands,
        IDraftStateService drafts,
        IDraftQueryService queries,
        IAnalyticsService analytics,
        IAiProviderRegistry ai,
        IAiConfigStore aiConfigs,
        IAiUsageService usage,
        IAiResponseStore responses,
        IDraftChangeNotifier notifier,
        IFantasyDataWriter fantasyData,
        ITeamPortraitStore portraits,
        IFileSavePicker files,
        IMockDraftService mock,
        SessionState session)
    {
        _commands = commands;
        _drafts = drafts;
        _queries = queries;
        _analytics = analytics;
        _ai = ai;
        _aiConfigs = aiConfigs;
        _usage = usage;
        _responses = responses;
        _notifier = notifier;
        _fantasyData = fantasyData;
        _portraits = portraits;
        _files = files;
        _mock = mock;
        _session = session;
        _notifier.DraftChanged += OnDraftChanged;
    }

    public ObservableCollection<BoardRow> Board { get; } = [];
    [ObservableProperty] private IReadOnlyList<DraftBoardTeamHeader> _boardTeams = [];
    [ObservableProperty] private IReadOnlyList<DraftBoardRoundRow> _boardRounds = [];
    public ObservableCollection<PlayerRow> Available { get; } = [];
    public ObservableCollection<PlayerRow> Queue { get; } = [];
    public ObservableCollection<RosterSlotRow> Roster { get; } = [];
    public ObservableCollection<RosterSlotRow> OverviewRoster { get; } = [];
    public ObservableCollection<TauntTargetOption> RosterTeams { get; } = [];
    public ObservableCollection<TauntTargetOption> TauntTargets { get; } = [];
    public ObservableCollection<string> Alerts { get; } = [];
    public ObservableCollection<AiAnalystPanel> Analysts { get; } = [];
    public ObservableCollection<AiConversationTurn> Conversation { get; } = [];
    public ObservableCollection<string> DataSources { get; } = [];

    [ObservableProperty] private string _leagueName = "No draft open";
    [ObservableProperty] private string _locationLine = "Open or start a draft to see the current pick.";
    [ObservableProperty] private string _roundPick = "—";
    [ObservableProperty] private string _selectingTeam = "—";
    [ObservableProperty] private string? _selectingTeamKey;
    [ObservableProperty] private Bitmap? _selectedRosterPortrait;
    [ObservableProperty] private bool _hasSelectedRosterPortrait;
    [ObservableProperty] private string _userNext = "—";
    [ObservableProperty] private string _sourceMode = "Manual";
    [ObservableProperty] private bool _hasDraft;
    [ObservableProperty] private bool _queueEmpty = true;
    [ObservableProperty] private bool _rosterEmpty = true;
    [ObservableProperty] private bool _hasRosterBoard;
    [ObservableProperty] private string _rosterNeedsLine = "";
    [ObservableProperty] private TauntTargetOption? _selectedRosterTeam;
    [ObservableProperty] private bool _hasMultipleRosterTeams;
    [ObservableProperty] private TauntTargetOption? _selectedTauntTarget;
    [ObservableProperty] private bool _hasTauntTarget;
    [ObservableProperty] private bool _hasAlerts;
    [ObservableProperty] private string _search = "";
    [ObservableProperty] private string _positionFilter = "All";
    [ObservableProperty] private PlayerRow? _selectedPlayer;
    [ObservableProperty] private string _aiPrompt = "";
    [ObservableProperty] private string _aiContextLine = "Advice uses this league's scoring and the closest cached ranks.";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiModeHint))]
    private bool _aiDeepMode;
    [ObservableProperty] private bool _autoAskEnabled = true;
    [ObservableProperty] private bool _hasMockSession;
    [ObservableProperty] private bool _isMockPlaying;
    [ObservableProperty] private bool _canPlayMock;
    [ObservableProperty] private bool _canReturnToLive;
    [ObservableProperty] private string _mockStatusLine = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AiTileHorizontal))]
    [NotifyPropertyChangedFor(nameof(AiTileVertical))]
    private bool _aiStackVertically;

    public bool AiTileHorizontal => !AiStackVertically;
    public bool AiTileVertical => AiStackVertically;

    public string AiModeHint =>
        AiDeepMode
            ? WatcherHint("Deep: same snapshot, longer look (next-pick board, intervening teams). Slower and costs more.")
            : Analysts.Any(panel => panel.Enabled && AiAnalysisMode.IsDeep(false, panel.Role))
                ? WatcherHint("Fast for most providers. Anyone set to Deep Advisor on AI Providers still uses Deep.")
                : WatcherHint("Fast: short answer from the current snapshot.");
    [ObservableProperty] private BoardRow? _currentBoardRow;
    [ObservableProperty] private int _stateVersion;
    [ObservableProperty] private bool _canUndo;
    [ObservableProperty] private bool _canRedo;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankHeader))]
    [NotifyPropertyChangedFor(nameof(NameHeader))]
    [NotifyPropertyChangedFor(nameof(PositionHeader))]
    [NotifyPropertyChangedFor(nameof(NflHeader))]
    [NotifyPropertyChangedFor(nameof(AdpHeader))]
    [NotifyPropertyChangedFor(nameof(ProjHeader))]
    private PlayerListSort _sortBy = PlayerListSort.Rank;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RankHeader))]
    [NotifyPropertyChangedFor(nameof(NameHeader))]
    [NotifyPropertyChangedFor(nameof(PositionHeader))]
    [NotifyPropertyChangedFor(nameof(NflHeader))]
    [NotifyPropertyChangedFor(nameof(AdpHeader))]
    [NotifyPropertyChangedFor(nameof(ProjHeader))]
    private bool _sortDescending;
    [ObservableProperty] private string _dataSource = "FantasyPros";
    [ObservableProperty] private bool _hasMultipleSources;

    public string RankHeader => SortLabel("Rk", PlayerListSort.Rank);
    public string NameHeader => SortLabel("Player", PlayerListSort.Name);
    public string PositionHeader => SortLabel("Pos", PlayerListSort.Position);
    public string NflHeader => SortLabel("NFL", PlayerListSort.NflTeam);
    public string AdpHeader => SortLabel("ADP", PlayerListSort.Adp);
    public string ProjHeader => SortLabel("Proj", PlayerListSort.ProjectedPoints);

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
            CanUndo = false;
            CanRedo = false;
            LeagueName = "No draft open";
            LocationLine = "Create a draft from Draft Order, or start a mock draft from Leagues.";
            StatusMessage = "Start or open a draft first.";
            CurrentBoardRow = null;
            BoardTeams = [];
            BoardRounds = [];
            Roster.Clear();
            OverviewRoster.Clear();
            RosterTeams.Clear();
            TauntTargets.Clear();
            HasRosterBoard = false;
            HasMultipleRosterTeams = false;
            RosterNeedsLine = "";
            HasTauntTarget = false;
            SelectedRosterTeam = null;
            SelectedTauntTarget = null;
            SelectingTeamKey = null;
            SetSelectedRosterPortrait(null);
            HasMockSession = false;
            CanPlayMock = false;
            CanReturnToLive = false;
            MockStatusLine = "";
            return;
        }

        var state = await _drafts.GetWorkingStateAsync(draftId, _session.BranchId);
        if (state is null)
        {
            CanUndo = false;
            CanRedo = false;
            StatusMessage = "Draft not found.";
            return;
        }

        HasDraft = true;
        _session.BranchId = state.ActiveBranch.BranchId;
        var policies = await _mock.GetPoliciesAsync(draftId, state.ActiveBranch.BranchId);
        var policyByTeam = policies.ToDictionary(policy => policy.TeamId);
        HasMockSession = policies.Any(policy => policy.IsCpu);
        CanReturnToLive = state.ActiveBranch.ParentBranchId is not null;
        StateVersion = state.Draft.CurrentStateVersion;
        SourceMode = state.Draft.SourceMode.ToString();
        CanUndo = state.ActiveSelections.Values.Any(selection => selection.Source != PickSource.Keeper);
        CanRedo = state.Redo is not null;
        var snapshot = await _analytics.GetSnapshotAsync(draftId, state.ActiveBranch.BranchId);
        LeagueName = state.League.Name;
        RoundPick = snapshot.CurrentRoundPick;
        var onClockTeam = snapshot.CurrentTeamId is { } teamId
            ? state.Teams.FirstOrDefault(t => t.TeamId.Equals(teamId))
            : null;
        MockSeatPolicy? onClockPolicy = null;
        if (onClockTeam is not null)
            policyByTeam.TryGetValue(onClockTeam.TeamId, out onClockPolicy);
        var team = onClockTeam is null
            ? null
            : MockPersonalityCatalog.LabelWithPersonality(onClockTeam.Label, onClockPolicy);
        SelectingTeam = team ?? "—";
        SelectingTeamKey = snapshot.CurrentTeamId?.ToString();
        var currentSlot = state.CurrentSlot;
        LocationLine = currentSlot is null
            ? $"{state.League.Name} · draft complete · {state.ActiveSelections.Count} picks"
            : $"{state.League.Name} · Round {currentSlot.Round} of {state.League.RoundCount} · Pick {snapshot.CurrentRoundPick} (overall {currentSlot.OverallPick}) · {team} is on the clock";
        UserNext = snapshot.UserNextRoundPick is null
            ? "You have no remaining pick"
            : snapshot.PicksUntilUser == 0
                ? "This is your pick"
                : $"Your next pick is {snapshot.UserNextRoundPick} · {snapshot.PicksUntilUser} pick(s) away";
        var userOnClock = snapshot.PicksUntilUser == 0 && currentSlot is not null;
        CanPlayMock = HasMockSession && !IsMockPlaying && !userOnClock && currentSlot is not null;
        MockStatusLine = !HasMockSession
            ? ""
            : IsMockPlaying
                ? "Playing CPU seats…"
                : currentSlot is null
                    ? "Practice draft complete. Live draft returns to the real board. Practice from here starts a new run."
                    : userOnClock
                        ? "Practice · your pick. Draft, then Play until my pick."
                        : $"Practice · {team} is on the clock.";

        Board.Clear();
        var context = new QueryContext { DraftId = draftId, BranchId = state.ActiveBranch.BranchId };
        var playersById = (await _drafts.GetPlayersAsync()).ToDictionary(p => p.PlayerId);
        var userTeam = state.League.UserTeamId;
        foreach (var slot in state.Slots.OrderBy(s => s.OverallPick))
        {
            state.ActiveSelections.TryGetValue(slot.OverallPick, out var selection);
            var slotTeam = state.Teams.FirstOrDefault(t => t.TeamId.Equals(slot.TeamId));
            policyByTeam.TryGetValue(slot.TeamId, out var slotPolicy);
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
                Team = slotTeam is null
                    ? ""
                    : MockPersonalityCatalog.LabelWithPersonality(slotTeam.Label, slotPolicy),
                TeamId = slot.TeamId,
                Player = playerName,
                Position = position,
                IsCurrent = isCurrent,
                IsMine = userTeam is { } mine && slot.TeamId.Equals(mine)
            });
        }

        CurrentBoardRow = Board.FirstOrDefault(row => row.IsCurrent)
                          ?? Board.LastOrDefault(row => row.Player.Length > 0);

        var gridPicks = new Dictionary<int, DraftGridPick>();
        foreach (var selection in state.ActiveSelections.Values)
        {
            playersById.TryGetValue(selection.PlayerId, out var pickPlayer);
            gridPicks[selection.OverallPick] = new DraftGridPick(
                selection.OverallPick,
                selection.TeamId,
                selection.Round,
                pickPlayer?.Name ?? selection.PlayerId.ToString(),
                pickPlayer?.PrimaryPosition.ToString() ?? "");
        }

        var grid = DraftGridBuilder.Build(
            state.Teams,
            state.Slots,
            gridPicks,
            currentSlot?.OverallPick,
            userTeam);
        BoardTeams = grid.Teams
            .Select(header =>
            {
                policyByTeam.TryGetValue(header.TeamId, out var headerPolicy);
                return new DraftBoardTeamHeader
                {
                    TeamId = header.TeamId,
                    Label = MockPersonalityCatalog.LabelWithPersonality(header.Label, headerPolicy),
                    IsMine = header.IsMine
                };
            })
            .ToList();
        BoardRounds = grid.Rounds
            .Select(round => new DraftBoardRoundRow
            {
                Round = round.Round,
                Cells = round.Cells.Select(DraftBoardCell.From).ToList()
            })
            .ToList();

        await EnsureDataSourcesAsync(FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots));
        var providerIds = await _fantasyData.GetProviderIdsAsync();
        var available = await _queries.GetAvailablePlayersAsync(context, new PlayerFilter
        {
            Search = string.IsNullOrWhiteSpace(Search) ? null : Search,
            Position = Enum.TryParse<PlayerPosition>(PositionFilter, out var pos) ? pos : null,
            MaxResults = 80,
            SortBy = SortBy,
            SortDescending = SortDescending,
            SourceKey = SourceKeyFor(DataSource)
        });
        Available.Clear();
        foreach (var player in available.Players)
        {
            var playerId = PlayerId.Parse(player.PlayerId);
            string? sleeperId = null;
            string? yahooId = null;
            if (providerIds.TryGetValue(playerId, out var ids))
            {
                ids.TryGetValue("sleeper", out sleeperId);
                ids.TryGetValue("yahoo", out yahooId);
            }

            var links = PlayerExternalLinks.Build(player.Name, player.Position, sleeperId, yahooId);
            Available.Add(new PlayerRow
            {
                PlayerId = playerId,
                Rank = player.OverallRank,
                Name = player.Name,
                Position = player.Position,
                NflTeam = player.NflTeam,
                Adp = player.AdpRoundPick,
                Proj = player.ProjectedPoints,
                ProjLabel = player.ProjectedPoints is { } points ? points.ToString("0") : "—",
                Status = player.Status,
                StatusLabel = StatusCode(player.Status),
                InjuryDetail = player.InjuryLine ?? "",
                IsInjured = !string.Equals(player.Status, "Active", StringComparison.OrdinalIgnoreCase),
                Tier = player.Tier,
                HandcuffFor = player.HandcuffFor,
                FantasyProsUrl = links.FantasyPros,
                SleeperUrl = links.Sleeper,
                YahooUrl = links.Yahoo
            });
        }

        if (SelectedPlayer is null || Available.All(p => !p.PlayerId.Equals(SelectedPlayer.PlayerId)))
            SelectedPlayer = Available.FirstOrDefault();

        Queue.Clear();
        var queueItems = await _drafts.GetQueueAsync(draftId, state.ActiveBranch.BranchId);
        for (var i = 0; i < queueItems.Count; i++)
        {
            var item = queueItems[i];
            playersById.TryGetValue(item.PlayerId, out var queuedPlayer);
            Queue.Add(new PlayerRow
            {
                PlayerId = item.PlayerId,
                QueueItemId = item.QueueItemId,
                Rank = i + 1,
                Name = queuedPlayer?.Name ?? item.PlayerId.ToString(),
                Position = queuedPlayer?.PrimaryPosition.ToString() ?? "",
                NflTeam = queuedPlayer?.NflTeam ?? "",
                CanMoveUp = i > 0,
                CanMoveDown = i < queueItems.Count - 1
            });
        }

        var previousRosterTeam = SelectedRosterTeam?.TeamId;
        _suppressRosterTeamChange = true;
        RosterTeams.Clear();
        foreach (var rosterTeam in state.Teams.OrderBy(item => item.DraftPosition).ThenBy(item => item.Label))
        {
            var isMine = userTeam is { } mine && rosterTeam.TeamId.Equals(mine);
            policyByTeam.TryGetValue(rosterTeam.TeamId, out var rosterPolicy);
            var rosterLabel = MockPersonalityCatalog.LabelWithPersonality(rosterTeam.Label, rosterPolicy);
            RosterTeams.Add(new TauntTargetOption
            {
                TeamId = rosterTeam.TeamId,
                Label = isMine ? $"{rosterLabel} (you)" : rosterLabel
            });
        }

        HasMultipleRosterTeams = RosterTeams.Count > 1;
        SelectedRosterTeam = RosterTeams.FirstOrDefault(team => previousRosterTeam is { } id && team.TeamId.Equals(id))
            ?? RosterTeams.FirstOrDefault(team => userTeam is { } mine && team.TeamId.Equals(mine))
            ?? RosterTeams.FirstOrDefault();
        _suppressRosterTeamChange = false;
        FillRosterBoard(state, playersById, SelectedRosterTeam?.TeamId ?? userTeam);

        var previousTarget = SelectedTauntTarget?.TeamId;
        TauntTargets.Clear();
        foreach (var other in state.Teams.Where(team => userTeam is not { } mine || !team.TeamId.Equals(mine)))
        {
            policyByTeam.TryGetValue(other.TeamId, out var tauntPolicy);
            TauntTargets.Add(new TauntTargetOption
            {
                TeamId = other.TeamId,
                Label = MockPersonalityCatalog.LabelWithPersonality(other.Label, tauntPolicy)
            });
        }
        SelectedTauntTarget = TauntTargets.FirstOrDefault(target => previousTarget is { } id && target.TeamId.Equals(id))
            ?? TauntTargets.FirstOrDefault(target => currentSlot is not null && target.TeamId.Equals(currentSlot.TeamId))
            ?? TauntTargets.FirstOrDefault();
        HasTauntTarget = SelectedTauntTarget is not null;

        Alerts.Clear();
        foreach (var alert in snapshot.Alerts)
            Alerts.Add(alert.Message);

        QueueEmpty = Queue.Count == 0;
        RosterEmpty = Roster.Count == 0;
        HasAlerts = Alerts.Count > 0;
        var format = FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots);
        var sourceKey = SourceKeyFor(DataSource);
        AiContextLine = $"Advice uses {FantasyDataSourcePicker.Describe(sourceKey, format)} ranks, ADP, and this league's scoring.";
        await RefreshAnalystsAsync();
        await ReactToBoardAsync(snapshot);
    }

    partial void OnSelectedRosterTeamChanged(TauntTargetOption? value)
    {
        if (_suppressRosterTeamChange || value is null)
            return;
        _ = ShowRosterForAsync(value.TeamId);
    }

    private async Task ShowRosterForAsync(TeamId teamId)
    {
        if (_session.DraftId is not { } draftId)
            return;
        var state = await _drafts.GetWorkingStateAsync(draftId, _session.BranchId);
        if (state is null)
            return;
        var playersById = (await _drafts.GetPlayersAsync()).ToDictionary(player => player.PlayerId);
        FillRosterBoard(state, playersById, teamId);
    }

    private void FillRosterBoard(
        DraftWorkingState state,
        IReadOnlyDictionary<PlayerId, Player> playersById,
        TeamId? teamId)
    {
        Roster.Clear();
        OverviewRoster.Clear();
        if (state.RosterSlots.Count == 0)
        {
            HasRosterBoard = false;
            RosterNeedsLine = "";
            return;
        }

        var drafted = teamId is { } id
            ? state.SelectionsForTeam(id).Select(selection =>
            {
                playersById.TryGetValue(selection.PlayerId, out var pickPlayer);
                return new RosterBoardPlayer(
                    pickPlayer?.Name ?? selection.PlayerId.ToString(),
                    pickPlayer?.PrimaryPosition ?? PlayerPosition.WR,
                    pickPlayer?.NflTeam ?? "",
                    $"{selection.Round}.{selection.RoundPick:00}",
                    selection.OverallPick);
            }).ToList()
            : [];
        var board = RosterBoardBuilder.Build(state.RosterSlots, drafted);
        foreach (var slot in board.Slots)
        {
            var row = new RosterSlotRow
            {
                SlotCode = slot.SlotCode,
                DisplayName = slot.DisplayName,
                Player = slot.IsFilled ? FilledRosterName(slot) : "Need",
                Detail = slot.IsFilled
                    ? slot.RoundPick ?? ""
                    : slot.Kind == SlotKind.Bench || slot.Kind == SlotKind.Inactive ? "open" : "empty",
                Position = slot.Position ?? "",
                IsFilled = slot.IsFilled,
                IsNeed = !slot.IsFilled && slot.Kind is SlotKind.Required or SlotKind.Flex,
                ShowInOverview = slot.IsFilled || slot.Kind is SlotKind.Required or SlotKind.Flex
            };
            Roster.Add(row);
            if (row.ShowInOverview)
                OverviewRoster.Add(row);
        }

        HasRosterBoard = true;
        RosterNeedsLine = board.NeedsLine;
        SetSelectedRosterPortrait(teamId);
    }

    private static string FilledRosterName(RosterBoardSlot slot)
    {
        var name = slot.Player ?? "";
        var bits = new List<string>();
        if (!string.IsNullOrWhiteSpace(slot.Position))
            bits.Add(slot.Position);
        if (!string.IsNullOrWhiteSpace(slot.NflTeam))
            bits.Add(slot.NflTeam);
        return bits.Count == 0 ? name : $"{name} · {string.Join(" · ", bits)}";
    }

    private void SetSelectedRosterPortrait(TeamId? teamId)
    {
        SelectedRosterPortrait?.Dispose();
        SelectedRosterPortrait = null;
        if (teamId is { } id && _portraits.ExistingPath(id) is { } path)
            SelectedRosterPortrait = new Bitmap(path);
        HasSelectedRosterPortrait = SelectedRosterPortrait is not null;
    }

    [RelayCommand]
    private async Task ExportSelectedRosterPortraitAsync()
    {
        if (SelectedRosterTeam is not { } team)
            return;
        var source = _portraits.ExistingPath(team.TeamId);
        if (source is null)
        {
            StatusMessage = $"No image for {team.Label} yet.";
            return;
        }

        var dest = await _files.PickSavePathAsync(
            TeamPortraitFiles.SuggestedFileName(team.Label, source),
            Path.GetExtension(source));
        if (dest is null)
            return;
        _portraits.CopyTo(team.TeamId, dest);
        StatusMessage = $"Saved {team.Label} to {dest}.";
    }

    [RelayCommand]
    private async Task PracticeFromHereAsync()
    {
        if (_session.DraftId is not { } draftId || IsMockPlaying)
            return;

        ResetAutoAsk();
        _muteExternalReload = true;
        try
        {
            var result = await _mock.StartPracticeAsync(draftId);
            if (result.BranchId is { } branchId)
                _session.BranchId = branchId;
            StatusMessage = result.Succeeded
                ? "Started a practice branch from the live draft. Play until your pick, or Step. Live draft takes you back."
                : result.Error;
        }
        finally
        {
            _muteExternalReload = false;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task ReturnToLiveAsync()
    {
        if (_session.DraftId is not { } draftId || IsMockPlaying)
            return;

        ResetAutoAsk();
        _muteExternalReload = true;
        try
        {
            var result = await _mock.ReturnToLiveAsync(draftId);
            if (result.BranchId is { } branchId)
                _session.BranchId = branchId;
            StatusMessage = result.Succeeded
                ? "Back on the live draft. Practice picks stay on that practice branch."
                : result.Error;
        }
        finally
        {
            _muteExternalReload = false;
        }

        await ReloadAsync();
    }

    [RelayCommand]
    private async Task PlayMockAsync()
    {
        if (_session.DraftId is not { } draftId || IsMockPlaying)
            return;

        IsMockPlaying = true;
        CanPlayMock = false;
        _pauseMock = false;
        _pendingBoardReaction = null;
        _suppressBoardReaction = true;
        _muteExternalReload = true;
        var made = 0;
        string? last = null;
        try
        {
            while (!_pauseMock)
            {
                var result = await _mock.SimulateNextAsync(draftId, _session.BranchId);
                if (result.IsUserPick)
                {
                    StatusMessage = made == 0
                        ? "This is your pick. Draft, then Play until my pick."
                        : $"CPU made {made} pick(s). This is your pick.";
                    break;
                }

                if (result.IsComplete)
                {
                    StatusMessage = made == 0 ? "The draft is complete." : $"CPU made {made} pick(s). Draft complete.";
                    break;
                }

                if (!result.Succeeded)
                {
                    StatusMessage = result.Error;
                    break;
                }

                made += result.PicksMade;
                last = $"{result.TeamName} ({result.Personality}) took {result.PlayerName}.";
                StatusMessage = last;
                await ReloadAsync();
            }

            if (_pauseMock)
                StatusMessage = last is null ? "Paused." : $"Paused after {made} pick(s). {last}";
        }
        finally
        {
            _pauseMock = false;
            _suppressBoardReaction = false;
            _muteExternalReload = false;
            IsMockPlaying = false;
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private void PauseMock() => _pauseMock = true;

    [RelayCommand]
    private async Task StepMockAsync()
    {
        if (_session.DraftId is not { } draftId || IsMockPlaying)
            return;

        var result = await _mock.SimulateNextAsync(draftId, _session.BranchId);
        StatusMessage = result.IsUserPick
            ? "This is your pick. Draft, then Play or Step the CPU."
            : result.IsComplete
                ? "The draft is complete."
                : result.Succeeded
                    ? $"{result.TeamName} ({result.Personality}) took {result.PlayerName}."
                    : result.Error;
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task DraftSelectedAsync()
    {
        if (SelectedPlayer is null || _session.DraftId is not { } draftId)
            return;

        var playerId = SelectedPlayer.PlayerId;
        var playerName = SelectedPlayer.Name;
        await RunDraftMutationAsync(async () =>
        {
            var result = await _commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, playerId));
            StatusMessage = result.Succeeded ? $"Drafted {playerName}." : result.Error;
            if (result.Succeeded && Search.Length > 0)
                Search = "";
        });
    }

    [RelayCommand]
    private async Task DraftQueueAsync()
    {
        if (Queue.Count == 0 || _session.DraftId is not { } draftId)
            return;
        var top = Queue[0];
        var playerName = top.Name;
        var playerId = top.PlayerId;
        var team = SelectingTeam;
        await RunDraftMutationAsync(async () =>
        {
            var result = await _commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, playerId));
            StatusMessage = result.Succeeded
                ? $"Drafted {playerName} from the queue for {team}."
                : result.Error;
        });
    }

    [RelayCommand]
    private async Task QueueSelectedAsync()
    {
        if (SelectedPlayer is null || _session.DraftId is not { } draftId)
            return;
        await _drafts.AddToQueueAsync(new QueueAddCommand(draftId, SelectedPlayer.PlayerId));
        StatusMessage = $"Queued {SelectedPlayer.Name}.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task RemoveQueuedAsync(PlayerRow? row)
    {
        if (row?.QueueItemId is not { } queueItemId || _session.DraftId is not { } draftId)
            return;
        await _drafts.RemoveFromQueueAsync(new QueueRemoveCommand(draftId, queueItemId));
        StatusMessage = $"Removed {row.Name} from the queue.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task MoveQueuedUpAsync(PlayerRow? row) => await MoveQueuedAsync(row, -1);

    [RelayCommand]
    private async Task MoveQueuedDownAsync(PlayerRow? row) => await MoveQueuedAsync(row, 1);

    private async Task MoveQueuedAsync(PlayerRow? row, int delta)
    {
        if (row?.QueueItemId is null || _session.DraftId is null)
            return;

        var ids = Queue.Where(item => item.QueueItemId is not null).Select(item => item.QueueItemId!.Value).ToList();
        var index = ids.FindIndex(id => id.Equals(row.QueueItemId.Value));
        var target = index + delta;
        if (index < 0 || target < 0 || target >= ids.Count)
            return;

        (ids[index], ids[target]) = (ids[target], ids[index]);
        await _drafts.ReorderQueueAsync(new QueueReorderCommand(_session.DraftId.Value, ids));
        StatusMessage = delta < 0 ? $"Moved {row.Name} up." : $"Moved {row.Name} down.";
        await ReloadAsync();
    }

    [RelayCommand]
    private async Task RollbackLastAsync()
    {
        if (_session.DraftId is not { } draftId)
            return;
        var state = await _drafts.GetWorkingStateAsync(draftId, _session.BranchId);
        var last = state?.ActiveSelections.Values
            .Where(selection => selection.Source != PickSource.Keeper)
            .Select(selection => selection.OverallPick)
            .DefaultIfEmpty(0)
            .Max() ?? 0;
        if (state is null || last == 0)
            return;
        await RunDraftMutationAsync(async () =>
        {
            var result = await _commands.RollbackAsync(new RollbackDraftCommand(draftId, last - 1));
            StatusMessage = result.Succeeded ? $"Undid pick {last}." : result.Error;
        });
    }

    [RelayCommand]
    private async Task RedoAsync()
    {
        if (_session.DraftId is not { } draftId)
            return;
        await RunDraftMutationAsync(async () =>
        {
            var result = await _commands.RedoAsync(new RedoDraftCommand(draftId));
            StatusMessage = result.Succeeded ? "Redid the last undo." : result.Error;
        });
    }

    [RelayCommand]
    private async Task AskAiAsync()
    {
        if (_session.DraftId is not { } draftId || _session.BranchId is not { } branchId)
            return;
        var prompt = string.IsNullOrWhiteSpace(AiPrompt) ? "Who should I take here?" : AiPrompt.Trim();
        await AskAdvisorsAsync(draftId, branchId, prompt, auto: false);
    }

    [RelayCommand]
    private async Task TauntManagerAsync()
    {
        if (_session.DraftId is not { } draftId || _session.BranchId is not { } branchId)
            return;
        if (SelectedTauntTarget is not { } target)
        {
            StatusMessage = "Pick a manager to taunt.";
            return;
        }

        if (Analysts.Count == 0)
            await RefreshAnalystsAsync();
        var enabled = Analysts.Where(panel => panel.Enabled && panel.IncludeInAsk).ToList();
        if (enabled.Count == 0)
        {
            StatusMessage = Analysts.Any(panel => panel.Enabled)
                ? "No analyst is checked. Tick ChatGPT, Claude, and/or Grok next to their names."
                : "No AI provider is enabled. Configure ChatGPT, Claude, or Grok under AI Providers.";
            return;
        }

        if (enabled.Any(IsBusy))
            return;

        var state = await _drafts.GetWorkingStateAsync(draftId, branchId);
        if (state is null)
            return;

        var playersById = (await _drafts.GetPlayersAsync()).ToDictionary(player => player.PlayerId);
        var rosterLines = state.SelectionsForTeam(target.TeamId).Select(selection =>
        {
            playersById.TryGetValue(selection.PlayerId, out var player);
            return $"{selection.Round}.{selection.RoundPick:00} {player?.PrimaryPosition} {player?.Name ?? selection.PlayerId.ToString()}";
        }).ToList();
        var needs = RosterRules.RemainingNeeds(
            state.RosterSlots,
            state.SelectionsForTeam(target.TeamId)
                .Select(selection => playersById.TryGetValue(selection.PlayerId, out var player)
                    ? player.PrimaryPosition
                    : (PlayerPosition?)null)
                .Where(position => position.HasValue)
                .Select(position => position!.Value)
                .ToList());
        var needLine = needs.Count == 0
            ? "no obvious holes"
            : string.Join(", ", needs.Where(need => need.Value > 0).Select(need => $"{need.Value} {need.Key}"));
        var rosterBlock = rosterLines.Count == 0 ? "(no picks yet)" : string.Join("\n", rosterLines);
        var prompt =
            $"""
            Taunt {target.Label}.
            Their roster so far:
            {rosterBlock}
            Remaining needs: {needLine}
            """;
        var styles = TauntStyles.Assign(enabled.Select(panel => panel.ProviderKey));
        var version = StateVersion;
        StatusMessage = $"Asking for a taunt of {target.Label}.";
        var tasks = enabled.Select(panel => AskOneAsync(
            panel,
            draftId,
            branchId,
            version,
            prompt,
            TauntStyles.PromptKind,
            styles.GetValueOrDefault(panel.ProviderKey, TauntStyles.Melville),
            target.Label));
        await Task.WhenAll(tasks);
    }

    private async Task AskOneAsync(
        AiAnalystPanel panel,
        DraftId draftId,
        BranchId branchId,
        int version,
        string prompt,
        string? promptKind = null,
        string? tauntStyle = null,
        string? tauntTarget = null)
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

        var taunt = string.Equals(promptKind, TauntStyles.PromptKind, StringComparison.OrdinalIgnoreCase);
        var watch = string.Equals(promptKind, DraftWatcherTrigger.PromptKind, StringComparison.OrdinalIgnoreCase);
        var deep = !taunt && !watch && AiAnalysisMode.IsDeep(AiDeepMode, panel.Role);
        panel.Status = taunt
            ? $"Generating · {TauntStyles.Title(tauntStyle ?? TauntStyles.Melville)}"
            : watch ? "Generating · Watch"
            : deep ? "Generating · Deep" : "Generating";
        panel.Response = "";
        var started = DateTimeOffset.UtcNow;
        try
        {
            await foreach (var chunk in adapter.StreamAnalysisAsync(new AiAnalysisRequest
            {
                DraftId = draftId,
                BranchId = branchId,
                StateVersion = version,
                Prompt = prompt,
                FastMode = !deep,
                Model = panel.Model,
                PromptKind = promptKind,
                TauntStyle = tauntStyle,
                TauntTarget = tauntTarget
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
                {
                    var stale = StateVersion != version
                        ? $"Stale (v{version} / current {StateVersion})"
                        : watch ? "Ready · Watch"
                        : deep ? "Ready · Deep" : "Ready";
                    panel.Status = stale;
                }
            }

            if (!string.IsNullOrWhiteSpace(panel.Response) && panel.Status is not "Failed")
            {
                await _responses.SaveAsync(new AiSavedResponse
                {
                    ResponseId = Guid.NewGuid().ToString("D"),
                    DraftId = draftId,
                    BranchId = branchId,
                    Provider = panel.ProviderKey,
                    Model = panel.Model ?? "",
                    AnalyzedStateVersion = version,
                    Prompt = prompt,
                    Body = panel.Response,
                    RequestStartedAt = started,
                    ResponseCompletedAt = DateTimeOffset.UtcNow
                });
                Conversation.Add(new AiConversationTurn
                {
                    Provider = panel.Title,
                    Prompt = prompt,
                    Response = panel.Response,
                    When = LocalClock.Format(started)
                });
                var spent = await _usage.EstimatedDraftSpendAsync(draftId, panel.ProviderKey);
                panel.SpendLabel = spent > 0 ? AiCostEstimate.Label(spent) : "";
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
        IReadOnlyList<AiSavedResponse> saved = [];
        if (_session.DraftId is { } draftId && _session.BranchId is { } branchId)
        {
            if (!Equals(_conversationDraft, draftId) || !Equals(_conversationBranch, branchId))
            {
                Conversation.Clear();
                _conversationDraft = draftId;
                _conversationBranch = branchId;
                _watchSeeded = false;
                _seenWatchKeys.Clear();
                ResetAutoAsk();
            }

            saved = await _responses.ListAsync(draftId, branchId);
            if (Conversation.Count == 0)
            {
                foreach (var turn in saved)
                {
                    Conversation.Add(new AiConversationTurn
                    {
                        Provider = Core.Ai.AiProviderCatalog.Find(turn.Provider)?.ProductName ?? turn.Provider,
                        Prompt = string.IsNullOrWhiteSpace(turn.Prompt) ? "(earlier question)" : turn.Prompt,
                        Response = turn.Body,
                        When = LocalClock.Format(turn.RequestStartedAt)
                    });
                }
            }
        }

        Analysts.Clear();
        foreach (var descriptor in Core.Ai.AiProviderCatalog.All)
        {
            var config = configs.FirstOrDefault(c => c.ProviderKey == descriptor.ProviderKey);
            existing.TryGetValue(descriptor.ProviderKey, out var prior);
            var last = saved.LastOrDefault(row => row.Provider == descriptor.ProviderKey);
            var placeholder = config?.Enabled == true
                ? "Ask when you are on the clock."
                : "Enable this provider under AI Providers.";
            var response = prior?.Response;
            if (string.IsNullOrWhiteSpace(response) || IsPlaceholder(response))
                response = string.IsNullOrWhiteSpace(last?.Body) ? placeholder : last!.Body;
            var spent = _session.DraftId is { } id
                ? await _usage.EstimatedDraftSpendAsync(id, descriptor.ProviderKey)
                : 0m;
            Analysts.Add(new AiAnalystPanel
            {
                ProviderKey = descriptor.ProviderKey,
                Title = descriptor.ProductName,
                Enabled = config?.Enabled == true,
                Model = config?.Model ?? descriptor.DefaultModel,
                Role = string.IsNullOrWhiteSpace(config?.Role) ? AiAnalysisMode.FastAdvisor : config.Role,
                SpendLimit = config?.PerDraftSpendLimit,
                IncludeInAsk = prior?.IncludeInAsk ?? true,
                Status = config?.Enabled == true
                    ? CopiedAnalystStatus(prior?.Status, last is null ? "Idle" : "Saved")
                    : "Disabled",
                Response = response ?? placeholder,
                SpendLabel = spent > 0 ? AiCostEstimate.Label(spent) : ""
            });
        }

        OnPropertyChanged(nameof(AiModeHint));
    }

    private async Task ReactToBoardAsync(AnalyticsSnapshot snapshot)
    {
        if (_suppressBoardReaction)
            return;
        if (_boardReactionBusy)
        {
            _pendingBoardReaction = snapshot;
            return;
        }
        if (_session.DraftId is not { } draftId || _session.BranchId is not { } branchId)
            return;

        _boardReactionBusy = true;
        try
        {
            var fresh = DraftWatcherTrigger.Unseen(snapshot.Alerts, _seenWatchKeys);
            if (!_watchSeeded)
            {
                foreach (var alert in snapshot.Alerts)
                    _seenWatchKeys.Add(DraftWatcherTrigger.Fingerprint(alert));
                _watchSeeded = true;
                fresh = [];
            }
            else
            {
                foreach (var alert in snapshot.Alerts)
                    _seenWatchKeys.Add(DraftWatcherTrigger.Fingerprint(alert));
            }

            var onClock = snapshot.PicksUntilUser == 0 && snapshot.UserNextRoundPick is not null;
            var watchTask = fresh.Count > 0
                ? WatchAsync(draftId, branchId, fresh)
                : Task.CompletedTask;
            var askTask = AutoAskTrigger.ShouldAsk(
                    AutoAskEnabled,
                    onClock,
                    branchId,
                    snapshot.CurrentOverallPick,
                    _autoAskBranch,
                    _lastAutoAskOverall)
                ? AskAdvisorsAsync(draftId, branchId, "Who should I take here?", auto: true, snapshot.CurrentOverallPick)
                : Task.CompletedTask;
            await Task.WhenAll(watchTask, askTask);
        }
        finally
        {
            _boardReactionBusy = false;
            if (_pendingBoardReaction is { } pending && !_suppressBoardReaction)
            {
                _pendingBoardReaction = null;
                await ReactToBoardAsync(pending);
            }
        }
    }

    private async Task AskAdvisorsAsync(DraftId draftId, BranchId branchId, string prompt, bool auto, int? overallPick = null)
    {
        if (Analysts.Count == 0)
            await RefreshAnalystsAsync();
        var advisors = Analysts.Where(panel => panel.Enabled && panel.IncludeInAsk && !AiAnalysisMode.IsWatcher(panel.Role)).ToList();
        if (advisors.Count == 0)
        {
            if (!auto)
            {
                StatusMessage = Analysts.Any(panel => panel.Enabled && panel.IncludeInAsk && AiAnalysisMode.IsWatcher(panel.Role))
                    ? "Checked providers are Draft Watchers. They speak on board events. Check a Fast or Deep Advisor to Ask."
                    : Analysts.Any(panel => panel.Enabled)
                        ? "No analyst is checked. Tick ChatGPT, Claude, and/or Grok next to their names."
                        : "No AI provider is enabled. Configure ChatGPT, Claude, or Grok under AI Providers.";
            }
            return;
        }

        if (advisors.Any(IsBusy))
            return;

        if (auto)
        {
            if (overallPick is { } pick)
            {
                _autoAskBranch = branchId;
                _lastAutoAskOverall = pick;
            }

            StatusMessage = "On the clock — asking advisors.";
        }
        var version = StateVersion;
        await Task.WhenAll(advisors.Select(panel => AskOneAsync(panel, draftId, branchId, version, prompt)));
    }

    private async Task WatchAsync(DraftId draftId, BranchId branchId, IReadOnlyList<DraftAlert> events)
    {
        if (Analysts.Count == 0)
            await RefreshAnalystsAsync();
        var watchers = Analysts.Where(panel => panel.Enabled && panel.IncludeInAsk && AiAnalysisMode.IsWatcher(panel.Role)).ToList();
        if (watchers.Count == 0 || watchers.Any(IsBusy))
            return;

        var prompt = string.Join("\n", events.Select(item => $"- {item.Message}"));
        StatusMessage = "Board event — asking the Draft Watcher.";
        var version = StateVersion;
        await Task.WhenAll(watchers.Select(panel => AskOneAsync(
            panel,
            draftId,
            branchId,
            version,
            prompt,
            DraftWatcherTrigger.PromptKind)));
    }

    private string WatcherHint(string core)
    {
        if (Analysts.Any(panel => panel.Enabled && AiAnalysisMode.IsWatcher(panel.Role)))
            return core + " Draft Watcher is on: it speaks when the board changes, not on Ask.";
        return core;
    }

    private void ResetAutoAsk()
    {
        _autoAskBranch = null;
        _lastAutoAskOverall = -1;
        _pendingBoardReaction = null;
    }

    private static string CopiedAnalystStatus(string? prior, string fallback)
    {
        if (string.IsNullOrWhiteSpace(prior) || prior.StartsWith("Generating", StringComparison.Ordinal))
            return fallback;
        return prior;
    }

    private static bool IsBusy(AiAnalystPanel panel) =>
        panel.Status.StartsWith("Generating", StringComparison.Ordinal);

    private static string StatusCode(string? status) => status switch
    {
        "Questionable" => "Q",
        "Doubtful" => "D",
        "Out" => "O",
        "InjuredReserve" => "IR",
        "PhysicallyUnableToPerform" => "PUP",
        "Suspended" => "SUS",
        "NonFootballInjury" => "NFI",
        _ => ""
    };

    private static bool IsPlaceholder(string text) =>
        text.StartsWith("Ask when", StringComparison.Ordinal)
        || text.StartsWith("Enable this provider", StringComparison.Ordinal);

    [RelayCommand]
    private async Task SortAvailableAsync(string? column)
    {
        if (!Enum.TryParse<PlayerListSort>(column, ignoreCase: true, out var sort))
            return;

        if (SortBy == sort)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortBy = sort;
            SortDescending = sort == PlayerListSort.ProjectedPoints;
        }

        await ReloadAsync();
    }

    private string SortLabel(string label, PlayerListSort column)
    {
        if (SortBy != column)
            return label;
        return SortDescending ? $"{label} ▼" : $"{label} ▲";
    }

    partial void OnSearchChanged(string value)
    {
        if (!_muteExternalReload)
            _ = ReloadAsync();
    }

    partial void OnPositionFilterChanged(string value)
    {
        if (!_muteExternalReload)
            _ = ReloadAsync();
    }

    [RelayCommand]
    private void UseFastAi() => AiDeepMode = false;

    [RelayCommand]
    private void UseDeepAi() => AiDeepMode = true;

    [RelayCommand]
    private void TileAiSideBySide() => AiStackVertically = false;

    [RelayCommand]
    private void TileAiStacked() => AiStackVertically = true;

    partial void OnDataSourceChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        _session.DataSourceKey = SourceKeyFor(value);
        if (!_muteExternalReload && !_suppressSourceReload)
            _ = ReloadFromSourceAsync();
    }

    private async Task ReloadFromSourceAsync()
    {
        await ReloadAsync();
        StatusMessage = $"Showing {DataSource} ranks, ADP, and projections from the local cache.";
    }

    private async Task EnsureDataSourcesAsync(FantasyDataFormat leagueFormat)
    {
        var keys = (await _fantasyData.GetSourceKeysAsync())
            .Where(key => !key.Equals("seed", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (keys.Count == 0)
            keys = ["sleeper"];

        var labels = keys.Select(LabelForSource).Distinct().ToList();
        HasMultipleSources = labels.Count > 1;

        var unchanged = DataSources.Count == labels.Count && labels.TrueForAll(DataSources.Contains);
        _suppressSourceReload = true;
        if (!unchanged)
        {
            DataSources.Clear();
            foreach (var label in labels)
                DataSources.Add(label);
        }

        var preferred = FantasyDataSourcePicker.ChooseDisplayLabel(
            labels,
            string.IsNullOrWhiteSpace(DataSource) ? null : DataSource,
            _session.DataSourceKey is { } saved ? LabelForSource(saved) : null,
            LabelForSource(leagueFormat.SourceKey));
        if (DataSource != preferred)
            DataSource = preferred;
        _session.DataSourceKey = SourceKeyFor(preferred);
        _suppressSourceReload = false;
    }

    private static string LabelForSource(string key)
    {
        var format = FantasyDataFormat.TryParseSourceKey(key);
        if (format is not null)
            return $"FantasyPros ({format.DisplayName})";
        return key.Trim().ToLowerInvariant() switch
        {
            "fantasypros" => "FantasyPros",
            "sleeper" => "Sleeper",
            "seed" => "Seed",
            var other => other
        };
    }

    private static string SourceKeyFor(string label)
    {
        if (label.StartsWith("FantasyPros (", StringComparison.Ordinal)
            && label.EndsWith(')'))
        {
            var inner = label["FantasyPros (".Length..^1];
            var superflex = inner.Contains("Superflex", StringComparison.OrdinalIgnoreCase);
            var scoring = inner.Contains("PPR", StringComparison.Ordinal) && !inner.Contains("Half", StringComparison.Ordinal)
                ? ConsensusScoring.Ppr
                : inner.Contains("Standard", StringComparison.Ordinal)
                    ? ConsensusScoring.Standard
                    : ConsensusScoring.HalfPpr;
            return new FantasyDataFormat(scoring, superflex).SourceKey;
        }

        return label.Trim().ToLowerInvariant() switch
        {
            "fantasypros" => "fantasypros",
            "sleeper" => "sleeper",
            "seed" => "seed",
            var other => other
        };
    }

    private async Task RunDraftMutationAsync(Func<Task> action)
    {
        _muteExternalReload = true;
        try
        {
            await action();
            await ReloadAsync();
        }
        finally
        {
            _muteExternalReload = false;
        }
    }

    private void OnDraftChanged(object? sender, DraftChangedEventArgs e)
    {
        if (_muteExternalReload)
            return;
        if (_session.DraftId is { } id && e.DraftId.Equals(id))
            _ = ReloadAsync();
    }
}
