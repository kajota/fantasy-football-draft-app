using System.Text.Json;
using System.Text.Json.Serialization;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Providers.FantasyData;

public sealed class SleeperPlayerDto
{
    [JsonPropertyName("player_id")] public string? PlayerId { get; init; }
    [JsonPropertyName("full_name")] public string? FullName { get; init; }
    [JsonPropertyName("first_name")] public string? FirstName { get; init; }
    [JsonPropertyName("last_name")] public string? LastName { get; init; }
    [JsonPropertyName("team")] public string? Team { get; init; }
    [JsonPropertyName("position")] public string? Position { get; init; }
    [JsonPropertyName("fantasy_positions")] public List<string>? FantasyPositions { get; init; }
    [JsonPropertyName("status")] public string? Status { get; init; }
    [JsonPropertyName("injury_status")] public string? InjuryStatus { get; init; }
    [JsonPropertyName("search_rank")] public int? SearchRank { get; init; }
    [JsonPropertyName("active")] public bool? Active { get; init; }
    [JsonPropertyName("bye_week")] public int? ByeWeek { get; init; }
    [JsonPropertyName("years_exp")] public int? YearsExp { get; init; }
    [JsonPropertyName("injury_body_part")] public string? InjuryBodyPart { get; init; }
    [JsonPropertyName("injury_notes")] public string? InjuryNotes { get; init; }
    [JsonPropertyName("injury_start_date")] public string? InjuryStartDate { get; init; }
}

public sealed class SleeperProjectionDto
{
    [JsonPropertyName("adp_ppr")] public double? AdpPpr { get; init; }
    [JsonPropertyName("adp_2qb")] public double? Adp2Qb { get; init; }
    [JsonPropertyName("pass_att")] public double? PassAtt { get; init; }
    [JsonPropertyName("pass_cmp")] public double? PassCmp { get; init; }
    [JsonPropertyName("pass_yd")] public double? PassYd { get; init; }
    [JsonPropertyName("pass_td")] public double? PassTd { get; init; }
    [JsonPropertyName("pass_int")] public double? PassInt { get; init; }
    [JsonPropertyName("rush_att")] public double? RushAtt { get; init; }
    [JsonPropertyName("rush_yd")] public double? RushYd { get; init; }
    [JsonPropertyName("rush_td")] public double? RushTd { get; init; }
    [JsonPropertyName("rec")] public double? Rec { get; init; }
    [JsonPropertyName("rec_yd")] public double? RecYd { get; init; }
    [JsonPropertyName("rec_td")] public double? RecTd { get; init; }
}

public static class SleeperCatalog
{
    public const int MaxSearchRank = 500;
    public static readonly string[] Positions = ["QB", "RB", "WR", "TE", "K", "DEF"];

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public static string DisplayName(SleeperPlayerDto player)
    {
        if (!string.IsNullOrWhiteSpace(player.FullName))
            return player.FullName.Trim();
        var combined = $"{player.FirstName} {player.LastName}".Trim();
        if (combined.Length > 0)
            return combined;
        return player.Team?.Trim() ?? player.PlayerId ?? "Unknown";
    }

    public static PlayerPosition? MapPosition(SleeperPlayerDto player)
    {
        var raw = player.Position;
        if (string.IsNullOrWhiteSpace(raw) && player.FantasyPositions is { Count: > 0 })
            raw = player.FantasyPositions[0];
        return raw?.ToUpperInvariant() switch
        {
            "QB" => PlayerPosition.QB,
            "RB" => PlayerPosition.RB,
            "WR" => PlayerPosition.WR,
            "TE" => PlayerPosition.TE,
            "K" => PlayerPosition.K,
            "DEF" or "DST" => PlayerPosition.DEF,
            _ => null
        };
    }

    public static PlayerStatus MapStatus(SleeperPlayerDto player)
    {
        var injury = player.InjuryStatus?.Trim();
        if (!string.IsNullOrWhiteSpace(injury))
            return MapStatusToken(injury);

        return MapStatusToken(player.Status);
    }

    public static bool Include(SleeperPlayerDto player, PlayerPosition position)
    {
        if (position == PlayerPosition.DEF)
            return !string.IsNullOrWhiteSpace(player.Team) || !string.IsNullOrWhiteSpace(player.PlayerId);

        if (player.SearchRank is null or < 1 or > MaxSearchRank)
            return false;
        return true;
    }

    public static double? ChooseAdp(SleeperProjectionDto? projection)
    {
        if (projection is null)
            return null;
        if (IsUsableAdp(projection.Adp2Qb))
            return projection.Adp2Qb;
        if (IsUsableAdp(projection.AdpPpr))
            return projection.AdpPpr;
        return null;
    }

    public static Player? ToPlayer(SleeperPlayerDto dto, DateTimeOffset now)
    {
        var position = MapPosition(dto);
        if (position is null || !Include(dto, position.Value))
            return null;

        var name = DisplayName(dto);
        var team = string.IsNullOrWhiteSpace(dto.Team) ? "FA" : dto.Team.Trim().ToUpperInvariant();
        return new Player
        {
            PlayerId = PlayerId.FromName(name, team, position.Value.ToString()),
            Name = name,
            NflTeam = team,
            PrimaryPosition = position.Value,
            EligiblePositions = [position.Value],
            ByeWeek = dto.ByeWeek,
            YearsExp = dto.YearsExp,
            Status = MapStatus(dto),
            StatusUpdatedAt = now,
            InjuryBodyPart = TrimOrNull(dto.InjuryBodyPart),
            InjuryNotes = TrimOrNull(dto.InjuryNotes),
            InjuryStartedOn = TrimOrNull(dto.InjuryStartDate)
        };
    }

    public static PlayerProjection ToProjection(PlayerId playerId, string sourceKey, SleeperProjectionDto stats, DateTimeOffset now) =>
        new()
        {
            PlayerId = playerId,
            SourceKey = sourceKey,
            PassingAttempts = stats.PassAtt ?? 0,
            Completions = stats.PassCmp ?? 0,
            PassingYards = stats.PassYd ?? 0,
            PassingTouchdowns = stats.PassTd ?? 0,
            Interceptions = stats.PassInt ?? 0,
            RushingAttempts = stats.RushAtt ?? 0,
            RushingYards = stats.RushYd ?? 0,
            RushingTouchdowns = stats.RushTd ?? 0,
            Receptions = stats.Rec ?? 0,
            ReceivingYards = stats.RecYd ?? 0,
            ReceivingTouchdowns = stats.RecTd ?? 0,
            CachedAt = now,
            SourceTimestamp = now
        };

    public static int TierFor(int overallRank) => Math.Max(1, ((overallRank - 1) / 12) + 1);

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool IsUsableAdp(double? value) =>
        value is > 0 and < 400;

    private static PlayerStatus MapStatusToken(string? raw)
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
}
