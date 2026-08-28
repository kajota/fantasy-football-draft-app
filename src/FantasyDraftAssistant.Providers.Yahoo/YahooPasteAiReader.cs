using System.Text.Json;
using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Yahoo;

namespace FantasyDraftAssistant.Providers.Yahoo;

/// Fallback for a paste the deterministic parser could not read — typically because
/// Yahoo restyled the settings page and the row labels moved. Asks a configured AI
/// provider for a strict JSON snapshot, then hands it to the same
/// <see cref="YahooLeagueMapper"/> the deterministic path uses.
public sealed class YahooPasteAiReader(
    IAiProviderRegistry providers,
    IAiModelOptions options)
{
    private const int MaxOutputTokens = 8000;

    private const string SystemPrompt = """
        You extract fantasy football league settings from text a user copied out of a
        Yahoo Fantasy web page. Reply with a single JSON object and nothing else — no
        prose, no markdown fence.

        Schema:
        {
          "name": string,                 // league name
          "season": number,               // 4-digit year
          "team_count": number,
          "is_auction": boolean,          // true only for auction drafts
          "is_keeper": boolean,
          "draft_type": string,           // "snake", "linear", or "custom"
          "draft_rounds": number|null,
          "roster": [ {"position": "QB"|"WR"|"RB"|"TE"|"W/R/T"|"Q/W/R/T"|"W/R"|"W/T"|"K"|"DEF"|"BN"|"IR", "count": number} ],
          "stats":  [ {"stat_id": number|null, "display_name": string, "value": number} ],
          "teams":  [ {"name": string, "owner_name": string|null} ]
        }

        Rules:
        - "roster" must total every roster spot including bench (BN) and IR.
        - "stats" values are POINTS PER UNIT. Yahoo writes yardage as "25 yards per
          point"; convert that to 0.04. "10 yards per point" is 0.1.
        - Use Yahoo's own row label verbatim for "display_name".
        - "teams" must be in the order they appear on the page.
        - Omit nothing you can see; use null only when the text genuinely lacks it.
        """;

    /// Every provider that is enabled and has a key, Fast Advisor first so the default
    /// selection is the one meant for quick work.
    public async Task<IReadOnlyList<YahooAiReaderOption>> ListAvailableAsync(CancellationToken cancellationToken = default) =>
        (await options.ListEnabledAsync(cancellationToken))
        .Select(choice => new YahooAiReaderOption
        {
            ProviderKey = choice.ProviderKey,
            Model = choice.Model,
            DisplayName = choice.DisplayName,
            Role = choice.Role
        })
        .ToList();

    public async Task<YahooPasteParseResult> ReadAsync(
        YahooPasteInput input,
        string providerKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var available = await ListAvailableAsync(cancellationToken);
        var chosen = available.FirstOrDefault(option =>
            option.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));
        if (chosen is null)
        {
            return Failed(
                "That AI provider is not enabled or has no API key. Set one up on AI Providers, " +
                "or fill the league in by hand on League Setup.");
        }

        var adapter = providers.Get(chosen.ProviderKey)!;
        var completion = await adapter.CompleteTextAsync(
            chosen.Model,
            SystemPrompt,
            BuildUserPrompt(input),
            MaxOutputTokens,
            cancellationToken);

        if (!completion.Succeeded)
            return Failed(completion.Error ?? "The AI provider returned no answer.");

        var leagueId = YahooPasteParser.ExtractLeagueId(input.LeagueUrlOrId)
            ?? YahooPasteParser.ExtractLeagueId(input.SettingsText);
        if (leagueId is null)
            return Failed("Could not work out the Yahoo league ID. Paste the league URL into the League URL box.");

        try
        {
            return BuildSnapshot(completion.Text, $"nfl.l.{leagueId}");
        }
        catch (Exception ex)
        {
            return Failed($"Could not read the AI answer: {ex.Message}");
        }
    }

    private static string BuildUserPrompt(YahooPasteInput input) =>
        $"""
         === LEAGUE SETTINGS PAGE ===
         {input.SettingsText}

         === TEAMS PAGE ===
         {input.TeamsText}
         """;

    private static YahooPasteParseResult BuildSnapshot(string json, string leagueKey)
    {
        using var doc = JsonDocument.Parse(ExtractJsonObject(json));
        var root = doc.RootElement;
        var warnings = new List<string>
        {
            "This league was read by the AI, not the built-in parser. Check the roster and scoring on League Setup before drafting."
        };

        var roster = new List<YahooRosterPosition>();
        foreach (var item in Array(root, "roster"))
        {
            var position = String(item, "position");
            var count = Int(item, "count") ?? 0;
            if (!string.IsNullOrWhiteSpace(position) && count > 0)
                roster.Add(new YahooRosterPosition { Position = position, Count = count });
        }

        var stats = new List<YahooStatModifier>();
        foreach (var item in Array(root, "stats"))
        {
            var value = Decimal(item, "value");
            if (value is null)
                continue;
            stats.Add(new YahooStatModifier
            {
                StatId = Int(item, "stat_id") ?? 0,
                Value = value.Value,
                DisplayName = String(item, "display_name")
            });
        }

        var teams = new List<YahooTeamSnapshot>();
        foreach (var item in Array(root, "teams"))
        {
            var name = String(item, "name");
            if (string.IsNullOrWhiteSpace(name))
                continue;
            var number = teams.Count + 1;
            teams.Add(new YahooTeamSnapshot
            {
                TeamKey = $"{leagueKey}.t.{number}",
                Name = name,
                OwnerName = String(item, "owner_name"),
                TeamNumber = number,
                IsCurrentUser = false
            });
        }

        if (teams.Count == 0)
            return Failed("The AI answer contained no teams.");

        return new YahooPasteParseResult
        {
            UsedAi = true,
            Warnings = warnings,
            Snapshot = new YahooLeagueSnapshot
            {
                LeagueKey = leagueKey,
                Name = String(root, "name") ?? "Yahoo League",
                Season = Int(root, "season") ?? DateTime.UtcNow.Year,
                TeamCount = Int(root, "team_count") ?? teams.Count,
                IsAuction = Bool(root, "is_auction") ?? false,
                IsKeeper = Bool(root, "is_keeper") ?? false,
                DraftTypeRaw = String(root, "draft_type"),
                DraftRounds = Int(root, "draft_rounds"),
                Roster = roster,
                Stats = stats,
                Teams = teams,
                Keepers = []
            }
        };
    }

    /// Models sometimes wrap the object in a fence or a sentence despite the prompt.
    private static string ExtractJsonObject(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static YahooPasteParseResult Failed(string message) =>
        new() { UsedAi = true, MissingSections = [message] };

    private static IEnumerable<JsonElement> Array(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];

    private static string? String(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Int(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed)
            ? parsed
            : null;

    private static decimal? Decimal(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var parsed)
            ? parsed
            : null;

    private static bool? Bool(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;
}
