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

    /// Yahoo's own settings-page row labels, normalised by
    /// <see cref="YahooPasteParser.Normalize"/>. The API path resolves scoring by
    /// numeric stat_id; a pasted settings page has no ids, only these labels.
    ///
    /// Order matters where Yahoo reuses a word across sections: the settings page
    /// lists offense before defense, and the paste parser keeps the first match
    /// per category, so "interceptions" (thrown) resolves before the D/ST row.
    public static readonly IReadOnlyDictionary<string, ScoringCategory> StatNames =
        new Dictionary<string, ScoringCategory>(StringComparer.Ordinal)
        {
            // Offense
            ["passing yards"] = ScoringCategory.PassingYard,
            ["pass yards"] = ScoringCategory.PassingYard,
            ["passing touchdowns"] = ScoringCategory.PassingTouchdown,
            ["passing touchdown"] = ScoringCategory.PassingTouchdown,
            ["pass td"] = ScoringCategory.PassingTouchdown,
            ["interceptions"] = ScoringCategory.Interception,
            ["interceptions thrown"] = ScoringCategory.Interception,
            ["rushing yards"] = ScoringCategory.RushingYard,
            ["rush yards"] = ScoringCategory.RushingYard,
            ["rushing touchdowns"] = ScoringCategory.RushingTouchdown,
            ["rushing touchdown"] = ScoringCategory.RushingTouchdown,
            ["rush td"] = ScoringCategory.RushingTouchdown,
            ["reception"] = ScoringCategory.Reception,
            ["receptions"] = ScoringCategory.Reception,
            ["points per reception"] = ScoringCategory.Reception,
            ["receiving yards"] = ScoringCategory.ReceivingYard,
            ["rec yards"] = ScoringCategory.ReceivingYard,
            ["receiving touchdowns"] = ScoringCategory.ReceivingTouchdown,
            ["receiving touchdown"] = ScoringCategory.ReceivingTouchdown,
            ["rec td"] = ScoringCategory.ReceivingTouchdown,
            ["2-point conversions"] = ScoringCategory.TwoPointConversion,
            ["2 point conversions"] = ScoringCategory.TwoPointConversion,
            ["two point conversions"] = ScoringCategory.TwoPointConversion,
            ["fumbles lost"] = ScoringCategory.FumbleLost,
            ["fumble lost"] = ScoringCategory.FumbleLost,

            // Kicker
            ["field goals 0-19 yards"] = ScoringCategory.FieldGoal0To19,
            ["field goals 0-19"] = ScoringCategory.FieldGoal0To19,
            ["field goals 20-29 yards"] = ScoringCategory.FieldGoal20To29,
            ["field goals 20-29"] = ScoringCategory.FieldGoal20To29,
            ["field goals 30-39 yards"] = ScoringCategory.FieldGoal30To39,
            ["field goals 30-39"] = ScoringCategory.FieldGoal30To39,
            ["field goals 40-49 yards"] = ScoringCategory.FieldGoal40To49,
            ["field goals 40-49"] = ScoringCategory.FieldGoal40To49,
            ["field goals 50+ yards"] = ScoringCategory.FieldGoal50Plus,
            ["field goals 50+"] = ScoringCategory.FieldGoal50Plus,
            ["point after attempt made"] = ScoringCategory.ExtraPoint,
            ["point after attempt"] = ScoringCategory.ExtraPoint,
            ["extra point made"] = ScoringCategory.ExtraPoint,
            ["extra point returned"] = ScoringCategory.ExtraPointReturned,

            // Defense / special teams
            ["sack"] = ScoringCategory.Sack,
            ["sacks"] = ScoringCategory.Sack,
            ["interception"] = ScoringCategory.DefensiveInterception,
            ["defensive interception"] = ScoringCategory.DefensiveInterception,
            ["fumble recovery"] = ScoringCategory.FumbleRecovery,
            ["fumble recoveries"] = ScoringCategory.FumbleRecovery,
            ["touchdown"] = ScoringCategory.DefensiveTouchdown,
            ["defensive touchdown"] = ScoringCategory.DefensiveTouchdown,
            ["safety"] = ScoringCategory.Safety,
            ["safeties"] = ScoringCategory.Safety,
            ["points allowed 0 points"] = ScoringCategory.PointsAllowed0,
            ["points allowed 0"] = ScoringCategory.PointsAllowed0,
            ["points allowed 1-6 points"] = ScoringCategory.PointsAllowed1To6,
            ["points allowed 1-6"] = ScoringCategory.PointsAllowed1To6,
            ["points allowed 7-13 points"] = ScoringCategory.PointsAllowed7To13,
            ["points allowed 7-13"] = ScoringCategory.PointsAllowed7To13,
            ["points allowed 14-20 points"] = ScoringCategory.PointsAllowed14To20,
            ["points allowed 14-20"] = ScoringCategory.PointsAllowed14To20,
            ["points allowed 21-27 points"] = ScoringCategory.PointsAllowed21To27,
            ["points allowed 21-27"] = ScoringCategory.PointsAllowed21To27,
            ["points allowed 28-34 points"] = ScoringCategory.PointsAllowed28To34,
            ["points allowed 28-34"] = ScoringCategory.PointsAllowed28To34,
            ["points allowed 35+ points"] = ScoringCategory.PointsAllowed35Plus,
            ["points allowed 35+"] = ScoringCategory.PointsAllowed35Plus
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

            // Pasted settings pages carry no stat_id, only Yahoo's row label.
            if (stat.StatId <= 0
                && StatNames.TryGetValue(YahooPasteParser.Normalize(stat.DisplayName), out var byName))
            {
                scoring[byName] = stat.Value;
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

        var draftedSpots = RosterRules.DraftedRosterSpots(roster);
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

    /// Yahoo's own "Draft Type" setting encodes live vs. offline vs. auction, not
    /// snake vs. linear, and its "Custom" draft-order label describes how starting
    /// seats were assigned (random vs. manual) rather than the round-to-round pick
    /// pattern — Yahoo leagues snake either way. So only an unambiguous linear
    /// signal overrides the default; everything else, "custom" included, is Snake.
    /// DraftType.Custom is left for the user to pick explicitly on Draft Order.
    public static DraftType MapDraftType(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return DraftType.Snake;

        return raw.Trim().ToLowerInvariant() switch
        {
            "linear" or "straight" or "sequential" => DraftType.Linear,
            _ => DraftType.Snake
        };
    }
}
