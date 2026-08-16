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

public partial class TeamRow : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string? _ownerName;
    [ObservableProperty] private int _draftPosition;
    [ObservableProperty] private string _portraitStatus = "";
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private bool _normalImage;
    [ObservableProperty] private bool _canChoosePortraitStyle;
    [ObservableProperty] private bool _hasPortrait;
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
    SessionState session,
    ITeamPortraitGenerator portraits,
    ITeamPortraitStore portraitStore,
    IFileSavePicker files) : PageViewModel
{
    private bool _loading;

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
    [ObservableProperty] private string _rosterSummary = "";
    [ObservableProperty] private string _scoringSummary = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveNotice))]
    private string _saveNotice = "";
    [ObservableProperty] private bool _isGeneratingPortraits;
    [ObservableProperty] private PortraitAiOption? _selectedPortraitProvider;

    public bool HasSaveNotice => !string.IsNullOrWhiteSpace(SaveNotice);

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
        if (league.Platform == FantasyPlatform.Yahoo)
        {
            StatusMessage = "Imported from Yahoo. Verify team names, first-round seats, and keepers before you start. A later Yahoo refresh will not overwrite draft order or keepers unless you ask it to.";
        }
        var userTeam = league.UserTeamId;
        foreach (var team in await leagues.GetTeamsAsync(id))
        {
            var isMine = userTeam is { } mine && team.TeamId.Equals(mine);
            Teams.Add(new TeamRow
            {
                TeamId = team.TeamId,
                Name = team.Name,
                OwnerName = team.OwnerName,
                DraftPosition = team.DraftPosition,
                ExternalTeamId = team.ExternalTeamId,
                CanChoosePortraitStyle = !isMine,
                HasPortrait = portraitStore.Exists(team.TeamId)
            });
        }

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

        await leagues.SaveLeagueDetailsAsync(id, LeagueName, Season, RoundCount, DraftGuidelines);
        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = id,
            UserTeamId = Teams.FirstOrDefault()?.TeamId,
            Teams = Teams.Select(t => new TeamDraftPosition
            {
                TeamId = t.TeamId,
                Name = t.Name,
                OwnerName = t.OwnerName,
                DraftPosition = t.DraftPosition,
                ExternalTeamId = t.ExternalTeamId
            }).ToList()
        });
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
        SaveNotice = "Saved. League settings are stored.";
        StatusMessage = SaveNotice;
    }

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
