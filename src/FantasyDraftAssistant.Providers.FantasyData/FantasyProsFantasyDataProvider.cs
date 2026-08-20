using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.FantasyData;

public sealed class FantasyProsFantasyDataProvider(
    HttpClient http,
    IFantasyDataWriter writer,
    ICredentialStore credentials,
    TimeSpan? minInterval = null) : IFantasyDataProvider
{
    public const string Key = FantasyProsCatalog.Key;
    public const string CredentialScope = "fantasydata";
    public const string CredentialKey = "fantasypros";

    private readonly TimeSpan _minInterval = minInterval ?? FantasyProsCatalog.MinRequestInterval;
    private DateTimeOffset _nextAllowed = DateTimeOffset.MinValue;

    public string ProviderKey => Key;
    public string DisplayName => "FantasyPros";
    public string Description =>
        "Expert ranks, tiers, and ADP for the open league's closest FantasyPros sheet (Standard / Half PPR / PPR, 1-QB or Superflex). Projections are re-scored with this league's rules. Premium key: 1 request/second, 500/day.";

    public async Task<FantasyDataRefreshResult> RefreshAsync(
        FantasyDataRefreshRequest request,
        CancellationToken cancellationToken)
    {
        var apiKey = await credentials.GetSecretAsync(CredentialScope, CredentialKey, cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return FantasyDataRefreshResult.Fail(
                "Save a FantasyPros API key on Player Data first. Premium personal keys work; this app does not use a My Playbook login.");
        }

        try
        {
            var season = request.Season > 0 ? request.Season : DateTime.UtcNow.Year;
            var format = new FantasyDataFormat(
                request.Scoring ?? ConsensusScoring.HalfPpr,
                request.Superflex ?? false);
            var now = DateTimeOffset.UtcNow;
            var ranked = await LoadRankingsAsync(apiKey, season, format, cancellationToken);
            if (ranked.Count == 0)
                return FantasyDataRefreshResult.Fail("FantasyPros returned no ranked players.");

            IReadOnlyDictionary<string, (string? YahooId, Core.Enums.PlayerStatus Status, int? ByeWeek)> extras =
                new Dictionary<string, (string? YahooId, Core.Enums.PlayerStatus Status, int? ByeWeek)>();
            try
            {
                extras = FantasyProsCatalog.ParsePlayers(await GetAsync(apiKey, "nfl/players", cancellationToken));
            }
            catch (HttpRequestException)
            {
            }

            IReadOnlyDictionary<string, double> adpById = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
            try
            {
                adpById = await LoadAdpAsync(apiKey, season, format, cancellationToken);
            }
            catch (HttpRequestException)
            {
            }

            Dictionary<string, FantasyProsProjectedPlayer> projections = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var row in FantasyProsCatalog.ParseProjections(
                             await GetAsync(apiKey, ProjectionsPath(season), cancellationToken)))
                {
                    projections[row.ExternalId] = row;
                    if (!string.IsNullOrWhiteSpace(row.Name))
                        projections[$"{row.Name}|{row.NflTeam}|{row.Position}"] = row;
                }
            }
            catch (HttpRequestException)
            {
            }

            var players = new List<Player>();
            var ids = new List<PlayerProviderId>();
            var rankings = new List<PlayerRanking>();
            var adp = new List<PlayerAdp>();
            var projectionRows = new List<PlayerProjection>();
            var seen = new HashSet<PlayerId>();

            foreach (var row in ranked.OrderBy(r => r.OverallRank))
            {
                extras.TryGetValue(row.ExternalId, out var extra);
                var player = FantasyProsCatalog.ToPlayer(row, now, extra.Status, extra.ByeWeek);
                if (!seen.Add(player.PlayerId))
                    continue;

                players.Add(player);
                ids.Add(new PlayerProviderId
                {
                    PlayerId = player.PlayerId,
                    ProviderKey = Key,
                    ExternalId = row.ExternalId
                });
                var yahooId = extra.YahooId ?? row.YahooId;
                if (!string.IsNullOrWhiteSpace(yahooId))
                {
                    ids.Add(new PlayerProviderId
                    {
                        PlayerId = player.PlayerId,
                        ProviderKey = FantasyProsCatalog.YahooKey,
                        ExternalId = yahooId
                    });
                }

                foreach (var sourceKey in new[] { format.SourceKey, Key })
                {
                    rankings.Add(new PlayerRanking
                    {
                        PlayerId = player.PlayerId,
                        SourceKey = sourceKey,
                        OverallRank = row.OverallRank,
                        PositionRank = row.PositionRank,
                        Tier = row.Tier,
                        RankMin = row.RankMin,
                        RankMax = row.RankMax,
                        RankStd = row.RankStd,
                        CachedAt = now,
                        SourceTimestamp = now
                    });
                }
                double? adpOverall = adpById.TryGetValue(row.ExternalId, out var fetchedAdp)
                    ? fetchedAdp
                    : row.Adp;
                if (adpOverall is { } adpValue)
                {
                    foreach (var sourceKey in new[] { format.SourceKey, Key })
                    {
                        adp.Add(new PlayerAdp
                        {
                            PlayerId = player.PlayerId,
                            SourceKey = sourceKey,
                            OverallAdp = adpValue,
                            CachedAt = now,
                            SourceTimestamp = now
                        });
                    }
                }

                if (TryProjection(projections, row) is { } stats)
                {
                    foreach (var sourceKey in new[] { format.SourceKey, Key })
                        projectionRows.Add(FantasyProsCatalog.ToProjection(player.PlayerId, stats, now, sourceKey));
                }
            }

            return await writer.WriteAsync(Key, players, ids, rankings, adp, projectionRows, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return FantasyDataRefreshResult.Fail($"FantasyPros refresh failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return FantasyDataRefreshResult.Fail($"FantasyPros refresh failed: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<FantasyProsRankedPlayer>> LoadRankingsAsync(
        string apiKey,
        int season,
        FantasyDataFormat format,
        CancellationToken cancellationToken)
    {
        var scoring = format.ScoringParam;
        var position = format.PositionParam;
        var paths = new[]
        {
            $"nfl/{season}/consensus-rankings?type=draft&scoring={scoring}&position={position}",
            $"nfl/{season}/consensus-rankings?scoring={scoring}&position={position}"
        };

        Exception? last = null;
        foreach (var path in paths)
        {
            try
            {
                var parsed = FantasyProsCatalog.ParseRankings(await GetAsync(apiKey, path, cancellationToken));
                if (parsed.Count > 0)
                    return parsed;
            }
            catch (HttpRequestException ex)
            {
                last = ex;
            }
        }

        if (last is not null)
            throw last;
        return [];
    }

    private async Task<IReadOnlyDictionary<string, double>> LoadAdpAsync(
        string apiKey,
        int season,
        FantasyDataFormat format,
        CancellationToken cancellationToken)
    {
        var scoring = format.ScoringParam;
        var position = format.PositionParam;
        var paths = new[]
        {
            $"nfl/{season}/consensus-rankings?type=ADP&scoring={scoring}&position={position}",
            $"nfl/{season}/consensus-rankings?type=ADP&scoring={scoring}&position=ALL"
        };

        foreach (var path in paths)
        {
            try
            {
                var parsed = FantasyProsCatalog.ParseAdp(await GetAsync(apiKey, path, cancellationToken));
                if (parsed.Count > 0)
                    return parsed;
            }
            catch (HttpRequestException)
            {
            }
        }

        return new Dictionary<string, double>();
    }

    /// <summary>
    /// Projections for every position, in one request.
    ///
    /// The position parameter is not optional here, whatever the docs imply: without it the
    /// endpoint answers with running backs only and no error, which left every quarterback,
    /// receiver, tight end, kicker and defence with no projected points and so no value over
    /// replacement. Measured against the live API: no parameter returns 131 rows, all RB;
    /// position=ALL returns 605 across all six positions.
    /// </summary>
    public static string ProjectionsPath(int season) =>
        $"nfl/{season}/projections?week=0&position=ALL";

    private static FantasyProsProjectedPlayer? TryProjection(
        IReadOnlyDictionary<string, FantasyProsProjectedPlayer> projections,
        FantasyProsRankedPlayer row)
    {
        if (projections.TryGetValue(row.ExternalId, out var byId))
            return byId;
        projections.TryGetValue($"{row.Name}|{row.NflTeam}|{row.Position}", out var byName);
        return byName;
    }

    private async Task<string> GetAsync(string apiKey, string path, CancellationToken cancellationToken)
    {
        var wait = _nextAllowed - DateTimeOffset.UtcNow;
        if (wait > TimeSpan.Zero)
            await Task.Delay(wait, cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.Accept.ParseAdd("application/json");

        using var response = await http.SendAsync(request, cancellationToken);
        _nextAllowed = DateTimeOffset.UtcNow + _minInterval;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var hint = (int)response.StatusCode == 401 || (int)response.StatusCode == 403
                ? " Check the API key saved on Player Data."
                : (int)response.StatusCode == 429
                    ? " FantasyPros rate-limited the key (1 request/second, 500/day)."
                    : "";
            throw new HttpRequestException($"HTTP {(int)response.StatusCode} for {path}.{hint} {Short(body)}");
        }

        return body;
    }

    private static string Short(string body)
    {
        var trimmed = body.ReplaceLineEndings(" ").Trim();
        return trimmed.Length <= 180 ? trimmed : trimmed[..180] + "…";
    }
}
