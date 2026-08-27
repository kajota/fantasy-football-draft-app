using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using FantasyDraftAssistant.Core.Engine;

namespace FantasyDraftAssistant.Core.Yahoo;

/// Turns text copied out of a signed-in Yahoo browser session into the same
/// <see cref="YahooLeagueSnapshot"/> the API path produces, so everything below
/// <see cref="YahooLeagueMapper"/> is shared between the two sources.
///
/// The parser is deliberately label-driven rather than position-driven: it looks
/// up Yahoo's own row labels ("Roster Positions", "Passing Yards") wherever they
/// appear, and reports what it could not find in
/// <see cref="YahooPasteParseResult.MissingSections"/> instead of guessing.
public static partial class YahooPasteParser
{
    private const int MinimumTeams = 2;

    public static YahooPasteParseResult Parse(YahooPasteInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var missing = new List<string>();
        var warnings = new List<string>();

        var settings = LabelIndex.Build(input.SettingsText);

        var leagueId = ExtractLeagueId(input.LeagueUrlOrId)
            ?? ExtractLeagueId(settings.Find("league id", "league id#", "league id #", "league key"));
        var leagueKey = leagueId is null ? null : $"nfl.l.{leagueId}";

        var name = settings.Find("league name", "name", "league");
        var season = ParseSeason(settings, input.SettingsText);
        var draftTypeRaw = settings.Find("draft type", "draft");
        var isAuction = draftTypeRaw is not null
            && draftTypeRaw.Contains("auction", StringComparison.OrdinalIgnoreCase);

        var roster = ParseRosterPositions(settings.Find("roster positions", "roster position", "roster"), warnings);
        if (roster.Count == 0)
            missing.Add("Roster Positions");

        var stats = ParseScoring(input.SettingsText);
        if (stats.Count == 0)
            missing.Add("Scoring / stat categories");

        var teams = ParseTeams(input.TeamsText, leagueKey ?? "nfl.l.0");
        if (teams.Count < MinimumTeams)
            missing.Add("Teams and managers");

        var teamCount = ParseInt(settings.Find("max teams", "number of teams", "teams", "league size"))
            ?? teams.Count;

        if (leagueKey is null)
        {
            missing.Add("League ID");
        }

        if (missing.Count > 0)
            return new YahooPasteParseResult { MissingSections = missing, Warnings = warnings };

        if (name is null)
        {
            name = $"Yahoo League {leagueId}";
            warnings.Add("Could not find the league name in the paste; using a placeholder. Rename it on League Setup.");
        }

        warnings.Add(
            $"The paste does not say which team is yours, so \"{teams[0].Name}\" was assumed. " +
            "Set the right one on League Setup.");

        if (teams.Count != teamCount)
        {
            warnings.Add($"Yahoo says {teamCount} teams but {teams.Count} were found in the Teams paste. Check for a missing row.");
            teamCount = teams.Count;
        }

        return new YahooPasteParseResult
        {
            Warnings = warnings,
            Snapshot = new YahooLeagueSnapshot
            {
                LeagueKey = leagueKey!,
                Name = name,
                Season = season,
                TeamCount = teamCount,
                IsAuction = isAuction,
                IsKeeper = LooksLikeKeeper(settings),
                DraftTypeRaw = draftTypeRaw,
                DraftRounds = ParseInt(settings.Find("draft rounds", "rounds")),
                Roster = roster,
                Stats = stats,
                Teams = teams,
                Keepers = []
            }
        };
    }

    // ---- league id -------------------------------------------------------

    public static string? ExtractLeagueId(string? urlOrId)
    {
        if (string.IsNullOrWhiteSpace(urlOrId))
            return null;

        var trimmed = urlOrId.Trim();

        // Already a full Yahoo key such as "nfl.l.123456".
        var keyMatch = LeagueKeyPattern().Match(trimmed);
        if (keyMatch.Success)
            return keyMatch.Groups[1].Value;

        // A league URL: .../f1/123456/... or ?league_id=123456
        var urlMatch = LeagueUrlPattern().Match(trimmed);
        if (urlMatch.Success)
            return urlMatch.Groups[1].Value;

        // A bare id, or a label value like "League ID# 123456".
        var digits = DigitRunPattern().Match(trimmed);
        return digits.Success ? digits.Groups[1].Value : null;
    }

    // ---- roster ----------------------------------------------------------

    private static IReadOnlyList<YahooRosterPosition> ParseRosterPositions(string? value, List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in value.Split([',', ';', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.Trim();
            if (token.Length == 0)
                continue;

            // "BN x 6", "BN (6)", "BN × 6" and plain "BN" all appear in the wild.
            var repeat = 1;
            var multiplier = RosterMultiplierPattern().Match(token);
            if (multiplier.Success)
            {
                token = multiplier.Groups[1].Value.Trim();
                repeat = int.Parse(multiplier.Groups[2].Value, CultureInfo.InvariantCulture);
            }

            var code = RosterRules.CanonicalSlotCode(token);
            if (code is "NA" or "N/A" || code.Length == 0)
                continue;

            var known = RosterRules.YahooSlotCatalog.Any(s =>
                string.Equals(s.SlotCode, code, StringComparison.OrdinalIgnoreCase));
            if (!known)
            {
                warnings.Add($"Roster slot \"{token}\" is not one this app knows. It was skipped.");
                continue;
            }

            counts[code] = counts.GetValueOrDefault(code) + repeat;
        }

        return counts
            .Select(kv => new YahooRosterPosition { Position = kv.Key, Count = kv.Value })
            .ToList();
    }

    // ---- scoring ---------------------------------------------------------

    /// Yahoo's scoring tables carry two numeric columns — "League Value" and
    /// "Yahoo Default Value" — and only the first is this league's actual scoring.
    ///
    /// A row whose league value differs from Yahoo's default also wraps across
    /// three lines, with the label, a marker, and the values each on their own:
    ///
    ///     Passing Touchdowns
    ///     Yahoo Default
    ///     &lt;tab&gt;6 &lt;tab&gt;4
    ///
    /// Reading such a row as a plain label/value pair silently yields Yahoo's
    /// default instead of the league's real setting, which is exactly the case
    /// that matters most (full PPR vs half, 6-point passing TDs).
    private static IReadOnlyList<YahooStatModifier> ParseScoring(string settingsText)
    {
        var stats = new List<YahooStatModifier>();
        var seen = new HashSet<Core.Enums.ScoringCategory>();
        string? pendingLabel = null;

        void Record(string label, string? value)
        {
            if (!YahooLeagueMapper.StatNames.TryGetValue(label, out var category))
                return;
            if (seen.Contains(category))
                return;

            var points = ParseStatValue(value);
            if (points is null)
                return;

            seen.Add(category);
            stats.Add(new YahooStatModifier { StatId = 0, Value = points.Value, DisplayName = label });
        }

        foreach (var raw in Lines(settingsText))
        {
            if (raw.Trim().Length == 0)
                continue;

            var cells = raw.Split('\t', StringSplitOptions.TrimEntries)
                .Where(cell => cell.Length > 0)
                .ToList();
            if (cells.Count == 0)
                continue;

            // Values-only continuation of a wrapped row. First cell is the
            // league value; the second is Yahoo's default and must never win.
            if (raw.StartsWith('\t') && pendingLabel is not null)
            {
                Record(pendingLabel, cells[0]);
                pendingLabel = null;
                continue;
            }

            var label = Normalize(cells[0]);

            if (cells.Count >= 2)
            {
                Record(label, cells[1]);
                pendingLabel = null;
                continue;
            }

            // A lone cell is either a wrapped stat label or the "Yahoo Default"
            // marker sitting between that label and its values — so only a known
            // stat name may replace what is already pending.
            if (YahooLeagueMapper.StatNames.ContainsKey(label))
                pendingLabel = label;
        }

        return stats;
    }

    /// Yahoo writes yardage as "25 yards per point" rather than "0.04".
    public static decimal? ParseStatValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var text = value.Trim();

        var perPoint = YardsPerPointPattern().Match(text);
        if (perPoint.Success
            && decimal.TryParse(perPoint.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var yards)
            && yards != 0)
        {
            return Math.Round(1m / yards, 4);
        }

        var pointPer = PointPerYardsPattern().Match(text);
        if (pointPer.Success
            && decimal.TryParse(pointPer.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var points)
            && decimal.TryParse(pointPer.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var per)
            && per != 0)
        {
            return Math.Round(points / per, 4);
        }

        var number = SignedNumberPattern().Match(text);
        return number.Success
               && decimal.TryParse(number.Groups[1].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var plain)
            ? plain
            : null;
    }

    // ---- teams -----------------------------------------------------------

    /// Yahoo's Managers page is a table — Team Name, Manager, Email, and several
    /// columns this app ignores. Two things make it awkward to read:
    ///
    ///   - the team-name cell carries the logo image's alt text ("logo Ginger Cool")
    ///   - a long team name or manager name wraps, so one record spans two lines
    ///
    /// The header row supplies the column count and the two indices needed, and the
    /// column count is what makes rejoining a wrapped row reliable: a record is only
    /// complete once it has as many cells as the header did. Reading line-by-line
    /// instead turns every wrapped row into a phantom extra team whose "name" is the
    /// tail of a manager's name and whose "owner" is their email address.
    private static IReadOnlyList<YahooTeamSnapshot> ParseTeams(string text, string leagueKey)
    {
        var pairs = ParseTeamsFromTable(text)
            ?? ParseTeamsFromManagerLabels(text);

        return pairs
            .Select((pair, i) => new YahooTeamSnapshot
            {
                TeamKey = $"{leagueKey}.t.{i + 1}",
                Name = pair.Team,
                OwnerName = pair.Owner,
                TeamNumber = i + 1,
                IsCurrentUser = false
            })
            .ToList();
    }

    private static List<(string Team, string? Owner)>? ParseTeamsFromTable(string text)
    {
        var lines = Lines(text).ToList();
        var header = -1;
        var columns = 0;
        var teamColumn = 0;
        var ownerColumn = 1;

        for (var i = 0; i < lines.Count && header < 0; i++)
        {
            var cells = lines[i].Split('\t').Select(Normalize).ToList();
            if (cells.Count < 2)
                continue;

            var team = cells.IndexOf("team name");
            var owner = cells.IndexOf("manager");
            if (team < 0 || owner < 0)
                continue;

            header = i;
            columns = cells.Count;
            teamColumn = team;
            ownerColumn = owner;
        }

        if (header < 0)
            return null;

        var pairs = new List<(string Team, string? Owner)>();
        var buffer = new StringBuilder();
        var bufferedLines = 0;

        for (var i = header + 1; i < lines.Count; i++)
        {
            if (buffer.Length == 0 && lines[i].Trim().Length == 0)
                continue;

            if (buffer.Length > 0)
                buffer.Append(' ');
            buffer.Append(lines[i]);
            bufferedLines++;

            var cells = buffer.ToString().Split('\t', StringSplitOptions.TrimEntries);
            if (cells.Length < columns)
            {
                // Past the end of the table the lines never complete a row, so
                // stop hoarding them rather than gluing page furniture together.
                if (bufferedLines >= 3)
                {
                    buffer.Clear();
                    bufferedLines = 0;
                }

                continue;
            }

            buffer.Clear();
            bufferedLines = 0;

            var name = DecodeEscapes(StripLogoPrefix(cells[teamColumn]));
            if (name.Length == 0 || IsChrome(name))
                continue;

            var owner = ownerColumn < cells.Length ? DecodeEscapes(cells[ownerColumn].Trim()) : null;
            pairs.Add((name, string.IsNullOrWhiteSpace(owner) ? null : owner));
        }

        return pairs.Count >= MinimumTeams ? pairs : null;
    }

    /// The team-name cell begins with the logo image's alt text.
    private static string StripLogoPrefix(string cell)
    {
        var value = cell.Trim();
        foreach (var prefix in LogoPrefixes)
        {
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                continue;
            var stripped = value[prefix.Length..].Trim();
            if (stripped.Length > 0)
                return stripped;
        }

        return value;
    }

    private static readonly string[] LogoPrefixes = ["team logo ", "logo ", "image "];

    /// Yahoo's Managers page emits some punctuation as an unresolved JavaScript
    /// escape, so a copied team name can literally contain the six characters
    /// \u2019 where a curly apostrophe belongs. These names get spoken back by the
    /// AI during a draft, so they are worth decoding rather than storing raw.
    private static string DecodeEscapes(string value) =>
        value.Contains("\\u", StringComparison.Ordinal)
            ? UnicodeEscapePattern().Replace(value, match =>
                ((char)int.Parse(match.Groups[1].ValueSpan, NumberStyles.HexNumber, CultureInfo.InvariantCulture))
                    .ToString())
            : value;

    /// Fallback for a copy that lost the table structure: Yahoo prefixes the
    /// manager with "Managers" / "Manager", so the line before it is the team.
    private static List<(string Team, string? Owner)> ParseTeamsFromManagerLabels(string text)
    {
        var lines = Lines(text).Where(l => l.Trim().Length > 0).ToList();
        var pairs = new List<(string Team, string? Owner)>();

        for (var i = 0; i < lines.Count; i++)
        {
            var match = ManagerLabelPattern().Match(lines[i].Trim());
            if (!match.Success)
                continue;

            var owner = match.Groups[1].Value.Trim();
            if (owner.Length == 0 && i + 1 < lines.Count)
                owner = lines[i + 1].Trim();

            var team = i > 0 ? lines[i - 1].Trim() : string.Empty;
            if (team.Length == 0 || IsChrome(team))
                continue;

            pairs.Add((team, owner.Length == 0 ? null : owner));
        }

        return pairs;
    }

    // ---- misc ------------------------------------------------------------

    private static bool LooksLikeKeeper(LabelIndex settings)
    {
        var keeper = settings.Find("keeper league", "keepers", "max keepers", "keeper settings");
        if (keeper is null)
            return false;
        if (keeper.Contains("no", StringComparison.OrdinalIgnoreCase) && keeper.Trim().Length <= 3)
            return false;
        return true;
    }

    /// The settings page has no "Season" row. The year only shows up incidentally —
    /// in the trade deadline and the footer copyright — and never near the top, so
    /// the whole document is scanned and the most frequent year wins.
    private static int ParseSeason(LabelIndex settings, string raw)
    {
        var labelled = ParseInt(settings.Find("season", "year"));
        if (labelled is >= 2000 and <= 2100)
            return labelled.Value;

        var years = new Dictionary<int, int>();
        var order = new List<int>();
        foreach (var line in Lines(raw))
        {
            foreach (Match match in SeasonPattern().Matches(line))
            {
                if (!int.TryParse(match.Groups[1].Value, out var year))
                    continue;
                if (!years.ContainsKey(year))
                    order.Add(year);
                years[year] = years.GetValueOrDefault(year) + 1;
            }
        }

        return order.Count == 0
            ? DateTime.UtcNow.Year
            : order.OrderByDescending(year => years[year]).First();
    }

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var match = DigitRunPattern().Match(value);
        return match.Success && int.TryParse(match.Groups[1].Value, out var parsed) ? parsed : null;
    }

    private static readonly string[] ChromeWords =
    [
        "team", "team name", "manager", "managers", "owner", "owners", "w-l-t", "waiver",
        "moves", "rank", "record", "points for", "points against", "standings", "scoreboard",
        "players", "draft", "league", "commissioner", "fantasy", "yahoo", "sign in", "sign out",
        "settings", "teams", "my team", "home", "search", "help", "more"
    ];

    private static bool IsChrome(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        var normalized = Normalize(value);
        return ChromeWords.Contains(normalized);
    }

    internal static IEnumerable<string> Lines(string text) =>
        (text ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

    /// Lowercase, collapse whitespace, drop trailing punctuation. Shared by the
    /// label index and by <see cref="YahooLeagueMapper.StatNames"/> lookups.
    internal static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var lastWasSpace = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsWhiteSpace(ch) || ch is ' ')
            {
                if (!lastWasSpace && builder.Length > 0)
                    builder.Append(' ');
                lastWasSpace = true;
                continue;
            }

            lastWasSpace = false;
            builder.Append(char.ToLowerInvariant(ch));
        }

        return builder.ToString().TrimEnd(':', '#', '.', '*', ' ');
    }

    [GeneratedRegex(@"^nfl\.l\.(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LeagueKeyPattern();

    [GeneratedRegex(@"(?:/f1/|league[_ ]?id[=# :]*)\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex LeagueUrlPattern();

    [GeneratedRegex(@"(\d{2,})")]
    private static partial Regex DigitRunPattern();

    [GeneratedRegex(@"^(.*?)\s*[x×(]\s*(\d+)\s*\)?$", RegexOptions.IgnoreCase)]
    private static partial Regex RosterMultiplierPattern();

    [GeneratedRegex(@"([\d.]+)\s*yards?\s*per\s*point", RegexOptions.IgnoreCase)]
    private static partial Regex YardsPerPointPattern();

    [GeneratedRegex(@"([\d.]+)\s*points?\s*per\s*([\d.]+)\s*yards?", RegexOptions.IgnoreCase)]
    private static partial Regex PointPerYardsPattern();

    [GeneratedRegex(@"(-?\+?\d+(?:\.\d+)?)")]
    private static partial Regex SignedNumberPattern();

    [GeneratedRegex(@"\\u([0-9a-fA-F]{4})")]
    private static partial Regex UnicodeEscapePattern();

    [GeneratedRegex(@"^managers?\s*[:\-]?\s*(.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex ManagerLabelPattern();

    [GeneratedRegex(@"\b(20\d{2})\b")]
    private static partial Regex SeasonPattern();

    /// Label/value pairs harvested from pasted text, tolerant of the two shapes a
    /// browser copy produces: tab-separated cells on one line, or a label line
    /// followed by its value on the next line.
    internal sealed class LabelIndex
    {
        private readonly Dictionary<string, string> _byLabel = new(StringComparer.Ordinal);

        public List<(string Label, string Value)> Pairs { get; } = [];

        public static LabelIndex Build(string text)
        {
            var index = new LabelIndex();
            var lines = Lines(text).ToList();

            for (var i = 0; i < lines.Count; i++)
            {
                var cells = lines[i].Split('\t', StringSplitOptions.TrimEntries)
                    .Where(cell => cell.Length > 0)
                    .ToList();

                if (cells.Count >= 2)
                {
                    index.Add(cells[0], string.Join(", ", cells.Skip(1)));
                    continue;
                }

                if (cells.Count != 1)
                    continue;

                // "Label: value" on a single line.
                var colon = cells[0].IndexOf(':');
                if (colon > 0 && colon < cells[0].Length - 1)
                {
                    index.Add(cells[0][..colon], cells[0][(colon + 1)..]);
                    continue;
                }

                // Label on its own line, value on the next non-empty line.
                var next = NextNonEmpty(lines, i);
                if (next is not null)
                    index.Add(cells[0], next);
            }

            return index;
        }

        private static string? NextNonEmpty(List<string> lines, int from)
        {
            for (var j = from + 1; j < lines.Count && j <= from + 2; j++)
            {
                var candidate = lines[j].Trim();
                if (candidate.Length > 0)
                    return candidate;
            }

            return null;
        }

        private void Add(string label, string value)
        {
            var key = Normalize(label);
            var text = value.Trim();
            if (key.Length == 0 || text.Length == 0)
                return;

            Pairs.Add((key, text));
            _byLabel.TryAdd(key, text);
        }

        /// First matching label wins, so callers pass their most specific alias first.
        public string? Find(params string[] labels)
        {
            foreach (var label in labels)
            {
                if (_byLabel.TryGetValue(Normalize(label), out var value))
                    return value;
            }

            return null;
        }
    }
}
