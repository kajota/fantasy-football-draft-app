using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Yahoo;

public static class YahooLeagueMapper
{
    public static readonly IReadOnlyDictionary<int, ScoringCategory> StatIds =
        new Dictionary<int, ScoringCategory>
        {
            [4] = ScoringCategory.PassingYard,
            [5] = ScoringCategory.PassingTouchdown,
            [6] = ScoringCategory.Interception,
            [9] = ScoringCategory.RushingYard,
            [10] = ScoringCategory.RushingTouchdown,
            [11] = ScoringCategory.Reception,
            [12] = ScoringCategory.ReceivingYard,
            [13] = ScoringCategory.ReceivingTouchdown,
            [16] = ScoringCategory.TwoPointConversion,
            [18] = ScoringCategory.FumbleLost,
            [19] = ScoringCategory.FieldGoal0To19,
            [20] = ScoringCategory.FieldGoal20To29,
            [21] = ScoringCategory.FieldGoal30To39,
            [22] = ScoringCategory.FieldGoal40To49,
            [23] = ScoringCategory.FieldGoal50Plus,
            [29] = ScoringCategory.ExtraPoint,
            [82] = ScoringCategory.ExtraPointReturned,
            [83] = ScoringCategory.ExtraPointReturned,
            [32] = ScoringCategory.Sack,
            [33] = ScoringCategory.DefensiveInterception,
            [34] = ScoringCategory.FumbleRecovery,
            [35] = ScoringCategory.DefensiveTouchdown,
            [36] = ScoringCategory.Safety,
            [50] = ScoringCategory.PointsAllowed0,
            [51] = ScoringCategory.PointsAllowed1To6,
            [52] = ScoringCategory.PointsAllowed7To13,
            [53] = ScoringCategory.PointsAllowed14To20,
            [54] = ScoringCategory.PointsAllowed21To27,
            [55] = ScoringCategory.PointsAllowed28To34,
            [56] = ScoringCategory.PointsAllowed35Plus
        };

    public static YahooMappedLeague Map(YahooLeagueSnapshot snapshot, YahooImportOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        options ??= new YahooImportOptions();

        if (snapshot.IsAuction)
        {
            throw new InvalidOperationException("Auction drafts are out of scope. Import a pick-based Yahoo league.");
        }

        if (snapshot.Teams.Count == 0)
        {
            throw new InvalidOperationException("Yahoo did not return any teams for this league.");
        }

        var roster = new List<RosterSlotSpec>();
        var unmappedRoster = new List<string>();
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var position in snapshot.Roster.Where(p => p.Count > 0))
        {
            var code = RosterRules.CanonicalSlotCode(position.Position);
            if (code is "NA" or "N/A")
                continue;

            var catalog = RosterRules.YahooSlotCatalog.FirstOrDefault(s =>
                string.Equals(s.SlotCode, code, StringComparison.OrdinalIgnoreCase)
                || string.Equals(s.YahooName, position.Position, StringComparison.OrdinalIgnoreCase));
            if (catalog is null)
            {
                unmappedRoster.Add($"{position.Position} × {position.Count}");
                continue;
            }

            counts[catalog.SlotCode] = counts.GetValueOrDefault(catalog.SlotCode) + position.Count;
        }

        foreach (var spec in RosterRules.FromCounts(counts))
        {
            roster.Add(new RosterSlotSpec
            {
                SlotCode = spec.SlotCode,
                SlotKind = spec.SlotKind,
                Count = spec.Count,
                EligiblePositions = spec.EligiblePositions
            });
        }

        if (roster.Count == 0)
        {
            foreach (var spec in RosterRules.DefaultYahooRoster())
            {
                roster.Add(new RosterSlotSpec
                {
                    SlotCode = spec.SlotCode,
                    SlotKind = spec.SlotKind,
                    Count = spec.Count,
                    EligiblePositions = spec.EligiblePositions
                });
            }
        }

        var scoring = ScoringCatalog.All.ToDictionary(info => info.Category, info => info.DefaultPoints);
        var unmappedScoring = new List<string>();
        foreach (var stat in snapshot.Stats)
        {
            if (StatIds.TryGetValue(stat.StatId, out var category))
            {
                scoring[category] = stat.Value;
                continue;
            }

            var label = string.IsNullOrWhiteSpace(stat.DisplayName)
                ? $"stat {stat.StatId}"
                : stat.DisplayName;
            unmappedScoring.Add($"{label} = {stat.Value}");
        }

        var scoringRules = scoring
            .Select(kv => new ScoringRuleSpec { Category = kv.Key, Points = kv.Value })
            .ToList();

        var teams = snapshot.Teams
            .OrderBy(t => t.TeamNumber)
            .ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .Select((team, index) => new ImportedTeamSpec
            {
                ExternalTeamId = team.TeamKey,
                Name = string.IsNullOrWhiteSpace(team.Name) ? $"Team {index + 1}" : team.Name.Trim(),
                OwnerName = string.IsNullOrWhiteSpace(team.OwnerName) ? null : team.OwnerName.Trim(),
                SuggestedDraftPosition = index + 1,
                IsUserTeam = team.IsCurrentUser
            })
            .ToList();

        if (teams.TrueForAll(t => !t.IsUserTeam))
            teams[0] = teams[0] with { IsUserTeam = true };

        var draftedSpots = roster.Where(s => s.SlotKind != SlotKind.Inactive).Sum(s => s.Count);
        var rounds = snapshot.DraftRounds is > 0
            ? snapshot.DraftRounds.Value
            : Math.Max(1, draftedSpots);

        var review = new List<string>
        {
            "Verify first-round seats on Draft Order. Yahoo team numbers are a starting guess, not a confirmed draft order."
        };
        if (snapshot.IsKeeper || snapshot.Keepers.Count > 0)
        {
            review.Add("This Yahoo league looks like a keeper league. Assign keepers on Keepers — they are not applied automatically.");
        }

        if (unmappedRoster.Count > 0)
            review.Add("Some Yahoo roster slots are not in this app (often IDP). Review the roster counts.");
        if (unmappedScoring.Count > 0)
            review.Add("Some Yahoo scoring stats were skipped. Review scoring on League Setup if those matter.");
        if (snapshot.Stats.Count == 0)
            review.Add("Yahoo did not return scoring modifiers. Half-PPR defaults were used.");
        if (snapshot.Roster.Count == 0)
            review.Add("Yahoo did not return roster slots. The Yahoo default roster was used.");

        return new YahooMappedLeague
        {
            Request = new ImportedLeagueRequest
            {
                Platform = FantasyPlatform.Yahoo,
                ExternalLeagueId = snapshot.LeagueKey,
                Name = string.IsNullOrWhiteSpace(snapshot.Name) ? "Yahoo League" : snapshot.Name.Trim(),
                Season = snapshot.Season > 0 ? snapshot.Season : DateTime.UtcNow.Year,
                DraftType = MapDraftType(snapshot.DraftTypeRaw),
                RoundCount = rounds,
                Teams = teams,
                Roster = roster,
                Scoring = scoringRules,
                ReplaceDraftOrder = options.ReplaceDraftOrder,
                SourcePreference = DraftSourcePreference.Yahoo
            },
            ReviewItems = review,
            UnmappedRosterSlots = unmappedRoster,
            UnmappedScoring = unmappedScoring
        };
    }

    public static DraftType MapDraftType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DraftType.Snake;

        return raw.Trim().ToLowerInvariant() switch
        {
            "linear" or "straight" or "sequential" => DraftType.Linear,
            "custom" => DraftType.Custom,
            _ => DraftType.Snake
        };
    }
}
