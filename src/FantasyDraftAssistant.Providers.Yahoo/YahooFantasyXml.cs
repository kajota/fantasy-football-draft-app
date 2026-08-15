using System.Globalization;
using System.Xml.Linq;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Providers.Yahoo;

public static class YahooFantasyXml
{
    public static IReadOnlyList<YahooLeagueSnapshot> ParseLeagueList(string xml)
    {
        var root = ParseRoot(xml);
        return root.Descendants().Where(IsLeague).Select(ParseLeagueElement).ToList();
    }

    public static YahooLeagueSnapshot ParseLeague(string xml)
    {
        var root = ParseRoot(xml);
        var league = root.Descendants().FirstOrDefault(IsLeague)
                     ?? throw new InvalidOperationException("Yahoo response did not contain a league.");
        return ParseLeagueElement(league);
    }

    public static YahooLeagueSnapshot Merge(YahooLeagueSnapshot primary, params YahooLeagueSnapshot[] extras)
    {
        var roster = primary.Roster;
        var stats = primary.Stats;
        var teams = primary.Teams;
        var keepers = primary.Keepers.ToList();
        var isAuction = primary.IsAuction;
        var isKeeper = primary.IsKeeper;
        var draftType = primary.DraftTypeRaw;
        var rounds = primary.DraftRounds;
        var teamCount = primary.TeamCount;
        var name = primary.Name;
        var season = primary.Season;

        foreach (var extra in extras)
        {
            if (extra.Roster.Count > 0)
                roster = extra.Roster;
            if (extra.Stats.Count > 0)
                stats = extra.Stats;
            if (extra.Teams.Count > 0)
                teams = extra.Teams;
            if (extra.Keepers.Count > 0)
                keepers.AddRange(extra.Keepers);
            isAuction = isAuction || extra.IsAuction;
            isKeeper = isKeeper || extra.IsKeeper;
            if (!string.IsNullOrWhiteSpace(extra.DraftTypeRaw))
                draftType = extra.DraftTypeRaw;
            if (extra.DraftRounds is > 0)
                rounds = extra.DraftRounds;
            if (extra.TeamCount > 0)
                teamCount = extra.TeamCount;
            if (!string.IsNullOrWhiteSpace(extra.Name))
                name = extra.Name;
            if (extra.Season > 0)
                season = extra.Season;
        }

        return new YahooLeagueSnapshot
        {
            LeagueKey = primary.LeagueKey,
            Name = name,
            Season = season,
            TeamCount = teamCount > 0 ? teamCount : teams.Count,
            IsAuction = isAuction,
            IsKeeper = isKeeper,
            DraftTypeRaw = draftType,
            DraftRounds = rounds,
            Roster = roster,
            Stats = stats,
            Teams = teams,
            Keepers = keepers
        };
    }

    private static XElement ParseRoot(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
            throw new InvalidOperationException("Yahoo returned an empty response.");
        try
        {
            return XDocument.Parse(xml).Root
                   ?? throw new InvalidOperationException("Yahoo returned XML without a root element.");
        }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidOperationException("Yahoo returned a response that was not valid XML.", ex);
        }
    }

    private static bool IsLeague(XElement element) =>
        Local(element) == "league" && Child(element, "league_key") is not null;

    private static YahooLeagueSnapshot ParseLeagueElement(XElement league)
    {
        var teams = league.Descendants().Where(e => Local(e) == "team" && Child(e, "team_key") is not null)
            .Select(ParseTeam)
            .ToList();

        var roster = league.Descendants()
            .Where(e => Local(e) == "roster_position" && Child(e, "position") is not null)
            .Select(ParseRosterPosition)
            .Where(p => p.Count > 0)
            .ToList();

        var stats = league.Descendants()
            .Where(e => Local(e) == "stat" && Child(e, "stat_id") is not null && Child(e, "value") is not null)
            .Select(ParseStat)
            .OfType<YahooStatModifier>()
            .ToList();

        var keepers = league.Descendants()
            .Where(e => Local(e) == "player" && HasKeeperFlag(e))
            .Select(ParseKeeper)
            .OfType<YahooKeeperHint>()
            .ToList();

        var settings = Child(league, "settings") ?? league;
        return new YahooLeagueSnapshot
        {
            LeagueKey = Text(league, "league_key") ?? "",
            Name = Text(league, "name") ?? "Yahoo League",
            Season = Int(league, "season") ?? 0,
            TeamCount = Int(league, "num_teams") ?? teams.Count,
            IsAuction = Flag(league, "is_auction_draft") || Flag(settings, "is_auction_draft"),
            IsKeeper = Flag(league, "is_keeper") || Flag(settings, "is_keeper") || Int(settings, "max_keepers") > 0,
            DraftTypeRaw = Text(settings, "draft_type") ?? Text(league, "draft_type"),
            DraftRounds = Int(settings, "draft_rounds") ?? Int(league, "draft_rounds"),
            Roster = roster,
            Stats = stats,
            Teams = teams,
            Keepers = keepers
        };
    }

    private static YahooTeamSnapshot ParseTeam(XElement team)
    {
        var key = Text(team, "team_key") ?? "";
        var manager = team.Descendants().FirstOrDefault(e => Local(e) == "manager");
        var number = Int(team, "team_id")
                     ?? TeamNumberFromKey(key);
        return new YahooTeamSnapshot
        {
            TeamKey = key,
            Name = Text(team, "name") ?? $"Team {number}",
            OwnerName = Text(manager, "nickname") ?? Text(manager, "guid"),
            TeamNumber = number,
            IsCurrentUser = Flag(manager, "is_current_login") || Flag(team, "is_owned_by_current_login")
        };
    }

    private static YahooRosterPosition ParseRosterPosition(XElement element) =>
        new()
        {
            Position = Text(element, "position") ?? "",
            Count = Int(element, "count") ?? 1
        };

    private static YahooStatModifier? ParseStat(XElement element)
    {
        var id = Int(element, "stat_id");
        var value = Decimal(element, "value");
        if (id is null || value is null)
            return null;
        return new YahooStatModifier
        {
            StatId = id.Value,
            Value = value.Value,
            DisplayName = Text(element, "name") ?? Text(element, "display_name")
        };
    }

    private static YahooKeeperHint? ParseKeeper(XElement player)
    {
        var name = Text(Child(player, "name"), "full")
                   ?? Text(player, "name")
                   ?? Text(player, "display_name");
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var team = player.Ancestors().FirstOrDefault(e => Local(e) == "team");
        return new YahooKeeperHint
        {
            TeamKey = Text(team, "team_key") ?? "",
            PlayerName = name,
            YahooPlayerId = Text(player, "player_id") ?? Text(player, "player_key"),
            RoundCost = Int(player, "keeper_cost") ?? Int(player, "cost")
        };
    }

    private static bool HasKeeperFlag(XElement player) =>
        Flag(player, "is_keeper") || Flag(player, "is_undroppable") && Child(player, "keeper_cost") is not null;

    private static int TeamNumberFromKey(string teamKey)
    {
        var last = teamKey.Split('.').LastOrDefault();
        return last is not null && last.StartsWith("t", StringComparison.OrdinalIgnoreCase)
            ? int.TryParse(last[1..], out var n) ? n : 0
            : int.TryParse(last, out var raw) ? raw : 0;
    }

    private static string Local(XElement? element) => element?.Name.LocalName ?? "";

    private static XElement? Child(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => Local(e) == localName);

    private static string? Text(XElement? parent, string localName) =>
        Child(parent, localName)?.Value?.Trim();

    private static int? Int(XElement? parent, string localName)
    {
        var text = Text(parent, localName);
        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static decimal? Decimal(XElement? parent, string localName)
    {
        var text = Text(parent, localName);
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static bool Flag(XElement? parent, string localName)
    {
        var text = Text(parent, localName);
        return text is "1" or "true" or "True";
    }
}
