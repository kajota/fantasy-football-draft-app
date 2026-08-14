using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public Core.Ids.TeamId TeamId { get; init; }
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

public partial class LeagueSetupViewModel(ILeagueService leagues, SessionState session) : PageViewModel
{
    public ObservableCollection<TeamRow> Teams { get; } = [];
    public ObservableCollection<RosterSlotEditor> Roster { get; } = [];

    [ObservableProperty] private string _leagueName = "";
    [ObservableProperty] private int _season = 2026;
    [ObservableProperty] private int _roundCount = 15;
    [ObservableProperty] private string _rosterSummary = "";

    public override async Task OnNavigatedToAsync()
    {
        Title = "League Setup";
        Teams.Clear();
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
        foreach (var team in await leagues.GetTeamsAsync(id))
        {
            Teams.Add(new TeamRow
            {
                TeamId = team.TeamId,
                Name = team.Name,
                OwnerName = team.OwnerName,
                DraftPosition = team.DraftPosition
            });
        }

        var saved = await leagues.GetRosterSlotsAsync(id);
        var counts = RosterRules.CountsFromSlots(saved);
        RebuildEditors(counts);
    }

    [RelayCommand]
    private void ApplyYahooRoster() => ApplyPreset(RosterRules.DefaultYahooRoster(), "Yahoo default (1 QB, 2 WR, 2 RB, 1 TE, 1 W/R/T, K, DEF, 6 BN, 2 IR).");

    [RelayCommand]
    private void ApplyStandardRoster() => ApplyPreset(RosterRules.DefaultStandardRoster(), "Standard 1-QB preset.");

    [RelayCommand]
    private void ApplySuperflexRoster() => ApplyPreset(RosterRules.DefaultSuperflexRoster(), "Superflex preset (adds one Q/W/R/T).");

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

        await leagues.SaveLeagueDetailsAsync(id, LeagueName, Season, RoundCount);
        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = id,
            UserTeamId = Teams.FirstOrDefault()?.TeamId,
            Teams = Teams.Select(t => new TeamDraftPosition
            {
                TeamId = t.TeamId,
                Name = t.Name,
                OwnerName = t.OwnerName,
                DraftPosition = t.DraftPosition
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

        session.LeagueName = LeagueName;
        StatusMessage = $"Saved. {RosterSummary}";
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
    }
}
