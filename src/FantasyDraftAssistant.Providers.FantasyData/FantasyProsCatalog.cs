using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Providers.FantasyData;

public sealed class FantasyProsRankedPlayer
{
    public required string ExternalId { get; init; }
    public required string Name { get; init; }
    public required string NflTeam { get; init; }
    public required PlayerPosition Position { get; init; }
    public required int OverallRank { get; init; }
    public int? PositionRank { get; init; }
    public int? Tier { get; init; }
    public double? Adp { get; init; }
    public double? AverageRank { get; init; }
    public string? YahooId { get; init; }
    public PlayerStatus Status { get; init; } = PlayerStatus.Active;
}

public sealed class FantasyProsProjectedPlayer
{
    public required string ExternalId { get; init; }
    public string? Name { get; init; }
    public string? NflTeam { get; init; }
    public PlayerPosition? Position { get; init; }
    public double PassingAttempts { get; init; }
    public double Completions { get; init; }
    public double PassingYards { get; init; }
    public double PassingTouchdowns { get; init; }
    public double Interceptions { get; init; }
    public double RushingAttempts { get; init; }
    public double RushingYards { get; init; }
    public double RushingTouchdowns { get; init; }
    public double Targets { get; init; }
    public double Receptions { get; init; }
    public double ReceivingYards { get; init; }
    public double ReceivingTouchdowns { get; init; }
}

public static class FantasyProsCatalog
{
    public const string Key = "fantasypros";
    public const string YahooKey = "yahoo";
    public static readonly TimeSpan MinRequestInterval = TimeSpan.FromSeconds(1);

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static IReadOnlyDictionary<string, double> ParseAdp(string json)
    {
        var map = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in ParseRankings(json))
        {
            var value = row.Adp ?? row.AverageRank ?? row.OverallRank;
            if (value is > 0 and < 400)
                map[row.ExternalId] = value;
        }

        return map;
    }

    public static IReadOnlyList<FantasyProsRankedPlayer> ParseRankings(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var players = FindArray(doc.RootElement, "players") ?? [];
        var mapped = new List<FantasyProsRankedPlayer>();
        foreach (var item in players)
        {
            var parsed = ParseRanked(item);
            if (parsed is not null)
                mapped.Add(parsed);
        }

        return mapped;
    }

    public static IReadOnlyList<FantasyProsProjectedPlayer> ParseProjections(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var players = FindArray(doc.RootElement, "players")
                      ?? FindArray(doc.RootElement, "projections")
                      ?? [];
        var mapped = new List<FantasyProsProjectedPlayer>();
        foreach (var item in players)
        {
            var parsed = ParseProjected(item);
            if (parsed is not null)
                mapped.Add(parsed);
        }

        return mapped;
    }

    public static IReadOnlyDictionary<string, (string? YahooId, PlayerStatus Status, int? ByeWeek)> ParsePlayers(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var players = FindArray(doc.RootElement, "players") ?? [];
        var map = new Dictionary<string, (string? YahooId, PlayerStatus Status, int? ByeWeek)>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in players)
        {
            var id = FirstString(item, "player_id", "id", "fantasypros_id");
            if (string.IsNullOrWhiteSpace(id))
                continue;
            map[id] = (
                FirstString(item, "yahoo_id", "player_yahoo_id", "yahoo"),
                MapStatus(FirstString(item, "injury_status", "player_injury_status", "status")),
                FirstInt(item, "player_bye_week", "bye_week", "bye"));
        }

        return map;
    }

    public static Player ToPlayer(FantasyProsRankedPlayer row, DateTimeOffset now, PlayerStatus? status = null, int? byeWeek = null) =>
        new()
        {
            PlayerId = PlayerId.FromName(row.Name, row.NflTeam, row.Position.ToString()),
            Name = row.Name,
            NflTeam = row.NflTeam,
            PrimaryPosition = row.Position,
            EligiblePositions = [row.Position],
            ByeWeek = byeWeek,
            Status = status ?? row.Status,
            StatusUpdatedAt = now
        };

    public static PlayerProjection ToProjection(
        PlayerId playerId,
        FantasyProsProjectedPlayer stats,
        DateTimeOffset now,
        string? sourceKey = null) =>
        new()
        {
            PlayerId = playerId,
            SourceKey = sourceKey ?? Key,
            PassingAttempts = stats.PassingAttempts,
            Completions = stats.Completions,
            PassingYards = stats.PassingYards,
            PassingTouchdowns = stats.PassingTouchdowns,
            Interceptions = stats.Interceptions,
            RushingAttempts = stats.RushingAttempts,
            RushingYards = stats.RushingYards,
            RushingTouchdowns = stats.RushingTouchdowns,
            Targets = stats.Targets,
            Receptions = stats.Receptions,
            ReceivingYards = stats.ReceivingYards,
            ReceivingTouchdowns = stats.ReceivingTouchdowns,
            CachedAt = now,
            SourceTimestamp = now
        };

    public static PlayerPosition? MapPosition(string? raw) =>
        raw?.Trim().ToUpperInvariant() switch
        {
            "QB" => PlayerPosition.QB,
            "RB" => PlayerPosition.RB,
            "WR" => PlayerPosition.WR,
            "TE" => PlayerPosition.TE,
            "K" or "PK" => PlayerPosition.K,
            "DEF" or "DST" or "D/ST" => PlayerPosition.DEF,
            _ => null
        };

    public static PlayerStatus MapStatus(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return PlayerStatus.Active;
        var token = raw.Trim().ToUpperInvariant();
        if (token.Contains("QUESTION", StringComparison.Ordinal))
            return PlayerStatus.Questionable;
        if (token.Contains("DOUBT", StringComparison.Ordinal))
            return PlayerStatus.Doubtful;
        if (token is "O" || token.Contains("OUT", StringComparison.Ordinal))
            return PlayerStatus.Out;
        if (token.Contains("PUP", StringComparison.Ordinal))
            return PlayerStatus.PhysicallyUnableToPerform;
        if (token.Contains("SUSP", StringComparison.Ordinal))
            return PlayerStatus.Suspended;
        if (token.Contains("NFI", StringComparison.Ordinal))
            return PlayerStatus.NonFootballInjury;
        if (token.Contains("IR", StringComparison.Ordinal) || token.Contains("INJURED RESERVE", StringComparison.Ordinal))
            return PlayerStatus.InjuredReserve;
        return PlayerStatus.Active;
    }

    public static int? ParsePositionRank(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var digits = new string(raw.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
    }

    private static FantasyProsRankedPlayer? ParseRanked(JsonElement item)
    {
        var name = FirstString(item, "player_name", "name");
        var position = MapPosition(FirstString(item, "player_position_id", "position_id", "position", "pos"));
        var rank = FirstInt(item, "rank_ecr", "ecr", "rank", "overall_rank");
        if (string.IsNullOrWhiteSpace(name) || position is null || rank is null or < 1)
            return null;

        var team = FirstString(item, "player_team_id", "team_id", "team") ?? "FA";
        var externalId = FirstString(item, "player_id", "id") ?? $"{name}|{team}|{position}";
        return new FantasyProsRankedPlayer
        {
            ExternalId = externalId,
            Name = name.Trim(),
            NflTeam = team.Trim().ToUpperInvariant(),
            Position = position.Value,
            OverallRank = rank.Value,
            PositionRank = FirstInt(item, "pos_rank", "position_rank") ?? ParsePositionRank(FirstString(item, "pos_rank")),
            Tier = FirstInt(item, "tier", "player_tier", "ecr_tier"),
            Adp = FirstDouble(item, "player_adp", "adp", "rank_adp") is { } adp && adp is > 0 and < 400
                ? adp
                : null,
            AverageRank = FirstDouble(item, "rank_ave"),
            YahooId = FirstString(item, "yahoo_id", "player_yahoo_id")
        };
    }

    private static FantasyProsProjectedPlayer? ParseProjected(JsonElement item)
    {
        var externalId = FirstString(item, "player_id", "id");
        var name = FirstString(item, "player_name", "name");
        if (string.IsNullOrWhiteSpace(externalId) && string.IsNullOrWhiteSpace(name))
            return null;

        var stats = item.TryGetProperty("stats", out var nested) && nested.ValueKind == JsonValueKind.Object
            ? nested
            : item;
        return new FantasyProsProjectedPlayer
        {
            ExternalId = externalId ?? name!,
            Name = name,
            NflTeam = FirstString(item, "player_team_id", "team_id", "team"),
            Position = MapPosition(FirstString(item, "player_position_id", "position_id", "position")),
            PassingAttempts = FirstDouble(stats, "passing_att", "pass_att") ?? 0,
            Completions = FirstDouble(stats, "passing_cmp", "pass_cmp", "cmp", "completions") ?? 0,
            PassingYards = FirstDouble(stats, "passing_yds", "pass_yd", "pass_yds", "py") ?? 0,
            PassingTouchdowns = FirstDouble(stats, "passing_tds", "pass_td", "pass_tds") ?? 0,
            Interceptions = FirstDouble(stats, "passing_ints", "pass_int", "ints", "int") ?? 0,
            RushingAttempts = FirstDouble(stats, "rushing_att", "rush_att") ?? 0,
            RushingYards = FirstDouble(stats, "rushing_yds", "rush_yd", "rush_yds") ?? 0,
            RushingTouchdowns = FirstDouble(stats, "rushing_tds", "rush_td", "rush_tds") ?? 0,
            Targets = FirstDouble(stats, "rec_tgt", "targets", "tgt") ?? 0,
            Receptions = FirstDouble(stats, "rec", "receptions") ?? 0,
            ReceivingYards = FirstDouble(stats, "rec_yds", "receiving_yds", "rec_yd") ?? 0,
            ReceivingTouchdowns = FirstDouble(stats, "rec_tds", "receiving_tds", "rec_td") ?? 0
        };
    }

    private static JsonElement[]? FindArray(JsonElement root, string name)
    {
        if (TryGetArray(root, name, out var direct))
            return direct;
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var data))
        {
            if (TryGetArray(data, name, out var nested))
                return nested;
        }

        return null;
    }

    private static bool TryGetArray(JsonElement element, string name, out JsonElement[] items)
    {
        items = [];
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (property.Value.ValueKind != JsonValueKind.Array)
                return false;
            items = property.Value.EnumerateArray().ToArray();
            return true;
        }

        return false;
    }

    private static string? FirstString(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.String)
            {
                var text = value.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    return text.Trim();
            }
            else if (value.ValueKind is JsonValueKind.Number)
            {
                return value.ToString();
            }
        }

        return null;
    }

    private static int? FirstInt(JsonElement element, params string[] names)
    {
        var number = FirstDouble(element, names);
        return number is null ? null : (int)Math.Round(number.Value, MidpointRounding.AwayFromZero);
    }

    private static double? FirstDouble(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
                continue;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number))
                return number;
            if (value.ValueKind == JsonValueKind.String
                && double.TryParse(value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        if (element.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in element.EnumerateObject())
        {
            if (!property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                continue;
            value = property.Value;
            return value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined;
        }

        return false;
    }
}
