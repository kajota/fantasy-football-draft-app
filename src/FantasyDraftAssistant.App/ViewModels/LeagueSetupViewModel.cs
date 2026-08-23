using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.App.ViewModels;

public sealed record PersonalityChoice(string Title, MockPersonality? Personality)
{
    public override string ToString() => Title;
}

public partial class TeamRow : ObservableObject
{
    public static readonly IReadOnlyList<PersonalityChoice> PersonalityChoices =
    [
        new PersonalityChoice("Random", null),
        .. Enum.GetValues<MockPersonality>()
            .Select(personality => new PersonalityChoice(MockPersonalityCatalog.Title(personality), personality))
    ];

    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _ownerName;
    [ObservableProperty] private string _portraitNotes = "";
    [ObservableProperty] private int _draftPosition;
    [ObservableProperty] private string _seatText = "1";
    [ObservableProperty] private string _portraitStatus = "";
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _normalImage;
    [ObservableProperty] private bool _canChoosePortraitStyle;
    [ObservableProperty] private bool _hasPortrait;
    [ObservableProperty] private bool _canMoveUp;
    [ObservableProperty] private bool _canMoveDown;
    [ObservableProperty] private PersonalityChoice _selectedPersonality = PersonalityChoices[0];

    public IReadOnlyList<PersonalityChoice> PersonalityOptions => PersonalityChoices;

    public void SyncSeatText() => SeatText = DraftPosition.ToString(CultureInfo.InvariantCulture);
    public Core.Ids.TeamId TeamId { get; init; }
    public string TeamKey => TeamId.ToString();
    public string? ExternalTeamId { get; init; }
}

public partial class RosterSlotEditor : ObservableObject
{
    private readonly Action _changed;

    public RosterSlotEditor(YahooRosterSlot definition, int count, Action changed)
    {
        SlotCode = definition.SlotCode;
        DisplayName = definition.DisplayName;
        YahooName = definition.YahooName;
        Eligibility = string.Join(" / ", definition.EligiblePositions);
        SlotKind = definition.SlotKind;
        EligiblePositions = definition.EligiblePositions;
        MaxCount = definition.MaxCount;
        _count = count;
        _changed = changed;
    }

    public string SlotCode { get; }
    public string DisplayName { get; }
    public string YahooName { get; }
    public string Eligibility { get; }
    public SlotKind SlotKind { get; }
    public IReadOnlyList<PlayerPosition> EligiblePositions { get; }
    public int MaxCount { get; }

    [ObservableProperty] private int _count;

    [RelayCommand]
    private void Increment()
    {
        if (Count >= MaxCount)
            return;
        Count++;
        _changed();
    }

    [RelayCommand]
    private void Decrement()
    {
        if (Count <= 0)
            return;
        Count--;
        _changed();
    }
}

public partial class ScoringRuleEditor : ObservableObject
{
    public ScoringRuleEditor(ScoringCategoryInfo info, decimal points, Action changed)
    {
        Category = info.Category;
        Group = info.Group;
        Label = info.Label;
        Help = info.Help;
        _points = points;
        _pointsText = Format(points);
        _changed = changed;
    }

    private readonly Action _changed;

    public ScoringCategory Category { get; }
    public string Group { get; }
    public string Label { get; }
    public string Help { get; }

    [ObservableProperty] private decimal _points;
    [ObservableProperty] private string _pointsText = "";
    [ObservableProperty] private bool _hasInvalidPoints;

    partial void OnPointsChanged(decimal value)
    {
        var text = Format(value);
        if (PointsText != text)
            PointsText = text;
        _changed();
    }

    partial void OnPointsTextChanged(string value)
    {
        if (decimal.TryParse(value.Trim(), NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var points))
        {
            HasInvalidPoints = false;
            if (Points != points)
                Points = points;
            return;
        }

        HasInvalidPoints = true;
    }

    private static string Format(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);
}

public partial class ScoringGroup
{
    public required string Name { get; init; }
    public required IReadOnlyList<ScoringRuleEditor> Rules { get; init; }
}

public sealed class PortraitAiOption
{
    public required string Label { get; init; }
    public required string ProviderKey { get; init; }
    public override string ToString() => Label;
}

public partial class LeagueSetupViewModel(
    ILeagueService leagues,
    IDraftStateService drafts,
    SessionState session,
    ITeamPortraitGenerator portraits,
    ITeamPortraitStore portraitStore,
    IFileSavePicker files,
    IAppSettingsStore settings) : PageViewModel
{
    private bool _loading;
    private string _publishBaseUrl = BoardSlug.DefaultBaseUrl;

    public ObservableCollection<TeamRow> Teams { get; } = [];
    public ObservableCollection<RosterSlotEditor> Roster { get; } = [];
    public ObservableCollection<ScoringGroup> ScoringGroups { get; } = [];
    public IReadOnlyList<DraftGuidelinePreset> GuidelinePresets { get; } = DraftGuidelinePresets.All;

    public IReadOnlyList<PortraitAiOption> PortraitProviders { get; } =
    [
        new() { Label = "Grok", ProviderKey = AiProviderCatalog.Xai },
        new() { Label = "ChatGPT", ProviderKey = AiProviderCatalog.OpenAi }
    ];

    [ObservableProperty] private string _leagueName = "";
    [ObservableProperty] private int _season = 2026;
    [ObservableProperty] private int _roundCount = 15;
    [ObservableProperty] private string _draftGuidelines = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BoardPublicUrl))]
    private string _webSlug = "";
    [ObservableProperty] private bool _publishBoard;
    [ObservableProperty] private string _boardPublicUrl = "";
    [ObservableProperty] private string _rosterSummary = "";
    [ObservableProperty] private string _scoringSummary = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveNotice))]
    private string _saveNotice = "";
    [ObservableProperty] private bool _isGeneratingPortraits;
    [ObservableProperty] private bool _canReorderTeams = true;
    [ObservableProperty] private PortraitAiOption? _selectedPortraitProvider;

    public bool HasSaveNotice => !string.IsNullOrWhiteSpace(SaveNotice);

    partial void OnWebSlugChanged(string value) =>
        BoardPublicUrl = BoardSlug.PublicUrl(_publishBaseUrl, value) ?? "";

    private async Task RefreshBoardUrlAsync()
    {
        _publishBaseUrl = await settings.GetAsync(BoardSlug.BaseUrlSettingKey) ?? BoardSlug.DefaultBaseUrl;
        BoardPublicUrl = BoardSlug.PublicUrl(_publishBaseUrl, WebSlug) ?? "";
    }

    public override async Task OnNavigatedToAsync()
    {
        Title = "League Setup";
        Teams.Clear();
        SaveNotice = "";
        SelectedPortraitProvider ??= PortraitProviders[0];
        if (session.LeagueId is not { } id)
        {
            StatusMessage = "Select or create a league first.";
            return;
        }

        var league = await leagues.GetLeagueAsync(id);
        if (league is null)
            return;
        LeagueName = league.Name;
        Season = league.Season;
        RoundCount = league.RoundCount;
        DraftGuidelines = league.DraftGuidelines ?? "";
        WebSlug = league.BoardSlug ?? "";
        PublishBoard = league.PublishBoard;
        await RefreshBoardUrlAsync();
        if (league.Platform == FantasyPlatform.Yahoo)
        {
            StatusMessage = "Imported from Yahoo. Verify team names, first-round seats, and keepers before you start. A later Yahoo refresh will not overwrite draft order or keepers unless you ask it to.";
        }
        var listed = await leagues.ListDraftsAsync(id);
        var current = SessionDraft.SelectDraft(session.DraftId, listed);
        if (current is null || current.Status == DraftStatus.NotStarted)
        {
            CanReorderTeams = true;
        }
        else
        {
            var branches = await leagues.GetBranchesAsync(current.DraftId);
            var liveId = branches.FirstOrDefault(branch => branch.ParentBranchId is null)?.BranchId
                         ?? current.ActiveBranchId;
            var live = await drafts.GetWorkingStateAsync(current.DraftId, liveId);
            CanReorderTeams = live is not null
                              && KeeperRules.CanReorderSeats(current.Status, live.ActiveSelections.Values);
        }

        if (!CanReorderTeams)
            StatusMessage = "Regular picks are on the live board, so first-round seats are locked. Undo those picks or stay on the live timeline to change order.";
        var userTeam = league.UserTeamId;
        foreach (var team in await leagues.GetTeamsAsync(id))
        {
            var isMine = userTeam is { } mine && team.TeamId.Equals(mine);
            var row = new TeamRow
            {
                TeamId = team.TeamId,
                Name = team.Name,
                OwnerName = team.OwnerName,
                PortraitNotes = team.PortraitNotes ?? "",
                DraftPosition = team.DraftPosition,
                ExternalTeamId = team.ExternalTeamId,
                CanChoosePortraitStyle = !isMine,
                HasPortrait = portraitStore.Exists(team.TeamId),
                SelectedPersonality = TeamRow.PersonalityChoices
                    .First(choice => choice.Personality == team.PracticePersonality)
            };
            row.SyncSeatText();
            Teams.Add(row);
        }
        RefreshSeats();

        _loading = true;
        try
        {
            var saved = await leagues.GetRosterSlotsAsync(id);
            var counts = RosterRules.CountsFromSlots(saved);
            RebuildEditors(counts);

            var scoring = await leagues.GetScoringRulesAsync(id);
            RebuildScoring(scoring.ToDictionary(rule => rule.Category, rule => rule.Points));
        }
        finally
        {
            _loading = false;
        }
    }

    [RelayCommand]
    private void ApplyGuidelinePreset(DraftGuidelinePreset? preset)
    {
        if (preset is null)
            return;
        DraftGuidelines = preset.Body.Trim();
        StatusMessage = $"Filled draft guidelines with {preset.Title}. Edit the text if you want, then Save.";
    }

    [RelayCommand]
    private void ClearGuidelines()
    {
        DraftGuidelines = "";
        StatusMessage = "Cleared draft guidelines. Save to store the empty notes.";
    }

    [RelayCommand]
    private void ApplyYahooRoster() => ApplyPreset(RosterRules.DefaultYahooRoster(), "Yahoo default (1 QB, 2 WR, 2 RB, 1 TE, 1 W/R/T, K, DEF, 6 BN, 2 IR).");

    [RelayCommand]
    private void ApplyStandardRoster() => ApplyPreset(RosterRules.DefaultStandardRoster(), "Standard 1-QB preset.");

    [RelayCommand]
    private void ApplySuperflexRoster() => ApplyPreset(RosterRules.DefaultSuperflexRoster(), "Superflex preset (adds one Q/W/R/T).");

    [RelayCommand]
    private void ApplyStandardScoring() => ApplyScoringPreset(ScoringCatalog.Standard(), "Standard scoring (no PPR, 4-point pass TDs).");

    [RelayCommand]
    private void ApplyHalfPprScoring() => ApplyScoringPreset(ScoringCatalog.HalfPpr(), "Half PPR scoring (0.5 points per catch).");

    [RelayCommand]
    private void ApplyPprScoring() => ApplyScoringPreset(ScoringCatalog.Ppr(), "Full PPR scoring (1 point per catch).");

    [RelayCommand]
    private void MatchRoundsToRoster()
    {
        RoundCount = Math.Max(1, DraftedSpots());
        StatusMessage = $"Draft rounds set to {RoundCount} (IR does not use a draft pick).";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (session.LeagueId is not { } id)
            return;

        if (ScoringEditors().Any(rule => rule.HasInvalidPoints))
        {
            StatusMessage = "Scoring has a value that is not a number. Fix it before saving.";
            return;
        }

        if (!string.IsNullOrWhiteSpace(WebSlug) && !BoardSlug.TryNormalize(WebSlug, out _))
        {
            StatusMessage = "Web board slug may contain only lowercase letters, numbers, and hyphens.";
            return;
        }

        if (PublishBoard && string.IsNullOrWhiteSpace(WebSlug))
        {
            StatusMessage = "Set a web board slug before turning on publishing.";
            return;
        }

        await leagues.SaveLeagueDetailsAsync(id, LeagueName, Season, RoundCount, DraftGuidelines);
        await leagues.SaveBoardPublishAsync(id, WebSlug, PublishBoard);
        await RefreshBoardUrlAsync();
        await PersistTeamsAsync();
        await leagues.SaveRosterAsync(new SaveRosterRequest
        {
            LeagueId = id,
            Slots = CurrentPreset().Select(s => new RosterSlotSpec
            {
                SlotCode = s.SlotCode,
                SlotKind = s.SlotKind,
                Count = s.Count,
                EligiblePositions = s.EligiblePositions
            }).ToList()
        });

        await leagues.SaveScoringAsync(new SaveScoringRequest
        {
            LeagueId = id,
            Rules = ScoringEditors().Select(rule => new ScoringRuleSpec
            {
                Category = rule.Category,
                Points = rule.Points
            }).ToList()
        });

        session.LeagueName = LeagueName;
        SaveNotice = CanReorderTeams
            ? $"Saved. First-round seats and the Draft Room board ({RoundCount} rounds) were updated."
            : $"Saved. The Draft Room board now has {RoundCount} rounds.";
        StatusMessage = SaveNotice;
    }

    [RelayCommand]
    private void MoveTeamUp(TeamRow? row) => MoveTeam(row, -1);

    [RelayCommand]
    private void MoveTeamDown(TeamRow? row) => MoveTeam(row, 1);

    public void ApplySeat(TeamRow? row)
    {
        if (row is null)
            return;
        if (!CanReorderTeams
            || !int.TryParse(row.SeatText.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var seat))
        {
            row.SyncSeatText();
            return;
        }

        var from = Teams.IndexOf(row);
        var to = TeamSeatOrder.TargetIndex(seat, Teams.Count);
        if (from < 0 || !TeamSeatOrder.Move(Teams, from, to))
        {
            row.SyncSeatText();
            return;
        }

        RefreshSeats();
        StatusMessage = $"Moved {DisplayName(row)} to seat {row.DraftPosition}. Save to keep it.";
    }

    public void MoveTeamToIndex(TeamRow? row, int toIndex)
    {
        if (row is null || !CanReorderTeams)
            return;
        var from = Teams.IndexOf(row);
        if (from < 0 || !TeamSeatOrder.Move(Teams, from, toIndex))
            return;
        RefreshSeats();
        StatusMessage = $"Moved {DisplayName(row)} to seat {row.DraftPosition}. Save to keep it.";
    }

    private async Task PersistTeamsAsync()
    {
        if (session.LeagueId is not { } id)
            return;

        var seats = Teams.Select((t, index) => new TeamDraftPosition
        {
            TeamId = t.TeamId,
            Name = t.Name,
            OwnerName = t.OwnerName,
            PortraitNotes = string.IsNullOrWhiteSpace(t.PortraitNotes) ? null : t.PortraitNotes.Trim(),
            DraftPosition = index + 1,
            ExternalTeamId = t.ExternalTeamId,
            PracticePersonality = t.SelectedPersonality.Personality
        }).ToList();
        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = id,
            Teams = seats
        });

        if (!CanReorderTeams)
            return;

        var league = await leagues.GetLeagueAsync(id);
        if (league is null)
            return;
        foreach (var draft in await leagues.ListDraftsAsync(id))
        {
            if (draft.Status == DraftStatus.Completed)
                continue;
            var branches = await leagues.GetBranchesAsync(draft.DraftId);
            var liveId = branches.FirstOrDefault(branch => branch.ParentBranchId is null)?.BranchId
                         ?? draft.ActiveBranchId;
            var live = await drafts.GetWorkingStateAsync(draft.DraftId, liveId);
            if (live is null || !KeeperRules.CanReorderSeats(draft.Status, live.ActiveSelections.Values))
                continue;

            await leagues.SaveDraftOrderAsync(new SaveDraftOrderRequest
            {
                LeagueId = id,
                DraftId = draft.DraftId,
                DraftType = league.DraftType,
                Teams = seats
            });
        }
    }

    private void MoveTeam(TeamRow? row, int delta)
    {
        if (row is null || !CanReorderTeams)
            return;
        var from = Teams.IndexOf(row);
        MoveTeamToIndex(row, from + delta);
    }

    private void RefreshSeats()
    {
        for (var i = 0; i < Teams.Count; i++)
        {
            Teams[i].DraftPosition = i + 1;
            Teams[i].SyncSeatText();
            Teams[i].CanMoveUp = CanReorderTeams && i > 0;
            Teams[i].CanMoveDown = CanReorderTeams && i < Teams.Count - 1;
        }
    }

    private static string DisplayName(TeamRow row) =>
        string.IsNullOrWhiteSpace(row.Name) ? $"Team {row.DraftPosition}" : row.Name;

    [RelayCommand]
    private Task GenerateAllPortraitsAsync() => GeneratePortraitsAsync(Teams.ToList());

    [RelayCommand]
    private Task GenerateOnePortraitAsync(TeamRow? row) =>
        row is null ? Task.CompletedTask : GeneratePortraitsAsync([row]);

    [RelayCommand]
    private async Task ExportPortraitAsync(TeamRow? row)
    {
        if (row is null)
            return;
        var source = portraitStore.ExistingPath(row.TeamId);
        if (source is null)
        {
            StatusMessage = $"No image for {row.Name} yet.";
            return;
        }

        var dest = await files.PickSavePathAsync(
            TeamPortraitFiles.SuggestedFileName(row.Name, source),
            Path.GetExtension(source));
        if (dest is null)
            return;
        portraitStore.CopyTo(row.TeamId, dest);
        StatusMessage = $"Saved {row.Name} to {dest}.";
    }

    private async Task GeneratePortraitsAsync(IReadOnlyList<TeamRow> rows)
    {
        if (session.LeagueId is not { } leagueId || rows.Count == 0)
            return;
        if (IsGeneratingPortraits)
            return;

        await PersistTeamsAsync();

        var league = await leagues.GetLeagueAsync(leagueId);
        var userTeam = league?.UserTeamId ?? Teams.FirstOrDefault()?.TeamId;
        IsGeneratingPortraits = true;
        var done = 0;
        try
        {
            foreach (var row in rows)
            {
                row.IsGenerating = true;
                row.PortraitStatus = "Generating…";
                StatusMessage = $"Generating {++done} of {rows.Count}: {row.Name}";
                var isMine = userTeam is { } mine && row.TeamId.Equals(mine);
                var result = await portraits.GenerateAsync(new TeamPortraitRequest
                {
                    TeamId = row.TeamId,
                    TeamName = string.IsNullOrWhiteSpace(row.Name) ? $"Team {row.DraftPosition}" : row.Name,
                    OwnerName = row.OwnerName,
                    PortraitNotes = string.IsNullOrWhiteSpace(row.PortraitNotes) ? null : row.PortraitNotes.Trim(),
                    IsUserTeam = isMine,
                    NormalImage = !isMine && row.NormalImage,
                    ProviderKey = SelectedPortraitProvider?.ProviderKey
                });
                row.IsGenerating = false;
                row.HasPortrait = result.Succeeded || portraitStore.Exists(row.TeamId);
                row.PortraitStatus = result.Succeeded
                    ? "Ready"
                    : result.Error ?? "Failed";
                if (!result.Succeeded)
                    StatusMessage = $"{row.Name}: {row.PortraitStatus}";
            }

            if (rows.All(row => portraitStore.Exists(row.TeamId)))
                StatusMessage = rows.Count == 1
                    ? $"Image ready for {rows[0].Name}. Hover the team name."
                    : "Team images are ready. Hover a name to see the roast — yours is the handsome one.";
        }
        finally
        {
            IsGeneratingPortraits = false;
            foreach (var row in rows)
                row.IsGenerating = false;
        }
    }

    private void ApplyPreset(IReadOnlyList<RosterSlotSpecPreset> preset, string message)
    {
        var counts = preset.ToDictionary(s => s.SlotCode, s => s.Count);
        RebuildEditors(counts);
        RoundCount = Math.Max(1, DraftedSpots());
        StatusMessage = $"{message} Save to keep it.";
    }

    private void RebuildEditors(IReadOnlyDictionary<string, int> counts)
    {
        Roster.Clear();
        foreach (var definition in RosterRules.YahooSlotCatalog)
        {
            Roster.Add(new RosterSlotEditor(definition, counts.GetValueOrDefault(definition.SlotCode), RefreshSummary));
        }

        RefreshSummary();
    }

    private IReadOnlyList<RosterSlotSpecPreset> CurrentPreset() =>
        Roster
            .Where(s => s.Count > 0)
            .Select(s => new RosterSlotSpecPreset(s.SlotCode, s.SlotKind, s.Count, s.EligiblePositions))
            .ToList();

    private int DraftedSpots() => Roster.Where(s => s.SlotKind != SlotKind.Inactive).Sum(s => s.Count);

    private void ApplyScoringPreset(IReadOnlyList<ScoringPreset> preset, string message)
    {
        RebuildScoring(preset.ToDictionary(rule => rule.Category, rule => rule.Points));
        _ = PersistScoringAsync($"{message} Scoring is saved. Refresh FantasyPros so ranks match.");
    }

    private void RebuildScoring(IReadOnlyDictionary<ScoringCategory, decimal> points)
    {
        var wasLoading = _loading;
        _loading = true;
        try
        {
            ScoringGroups.Clear();
            foreach (var group in ScoringCatalog.All.GroupBy(info => info.Group))
            {
                ScoringGroups.Add(new ScoringGroup
                {
                    Name = group.Key,
                    Rules = group.Select(info =>
                        new ScoringRuleEditor(info, ScoringCatalog.Resolve(info, points), OnScoringChanged)).ToList()
                });
            }

            RefreshSummary();
        }
        finally
        {
            _loading = wasLoading;
        }
    }

    private void OnScoringChanged()
    {
        RefreshSummary();
        if (!_loading)
            _ = PersistScoringAsync();
    }

    private async Task PersistScoringAsync(string? message = null)
    {
        if (session.LeagueId is not { } id)
            return;
        if (ScoringEditors().Any(rule => rule.HasInvalidPoints))
        {
            SaveNotice = "";
            StatusMessage = "Scoring has a value that is not a number. Fix it to save.";
            return;
        }

        await leagues.SaveScoringAsync(new SaveScoringRequest
        {
            LeagueId = id,
            Rules = ScoringEditors().Select(rule => new ScoringRuleSpec
            {
                Category = rule.Category,
                Points = rule.Points
            }).ToList()
        });

        SaveNotice = message ?? "Scoring saved.";
        StatusMessage = SaveNotice;
    }

    private IEnumerable<ScoringRuleEditor> ScoringEditors() =>
        ScoringGroups.SelectMany(group => group.Rules);

    private void RefreshSummary()
    {
        var drafted = DraftedSpots();
        var ir = Roster.Where(s => s.SlotKind == SlotKind.Inactive).Sum(s => s.Count);
        var mapped = CurrentPreset().Select(s => new Core.Models.RosterSlot
        {
            RosterSlotId = Core.Ids.RosterSlotId.New(),
            LeagueId = Core.Ids.LeagueId.New(),
            SlotCode = s.SlotCode,
            SlotKind = s.SlotKind,
            Count = s.Count,
            EligiblePositions = s.EligiblePositions
        }).ToList();
        var qbDemand = RosterRules.QbDemand(mapped);
        var format = qbDemand >= 2 ? $"Superflex/multi-QB ({qbDemand} QB-eligible starters)" : "1-QB";
        RosterSummary = $"{format} · {drafted} drafted spots · {ir} IR · suggested rounds {Math.Max(1, drafted)}";

        var rules = ScoringEditors().Select(rule => new Core.Models.ScoringRule
        {
            ScoringRuleId = Core.Ids.ScoringRuleId.New(),
            LeagueId = Core.Ids.LeagueId.New(),
            Category = rule.Category,
            Points = rule.Points
        }).ToList();
        var profile = ScoringCatalog.ProfileName(rules, qbDemand >= 2);
        var fp = FantasyDataFormat.FromLeague(rules, mapped);
        ScoringSummary = $"{profile}. FantasyPros ranks use {fp.DisplayName}. AI sees these exact point values.";
    }
}
