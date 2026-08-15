using System.Net.Http.Json;
using System.Text.Json;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.FantasyData;

public sealed class SleeperFantasyDataProvider(HttpClient http, IFantasyDataWriter writer) : IFantasyDataProvider
{
    public const string Key = "sleeper";

    public string ProviderKey => Key;
    public string DisplayName => "Sleeper";
    public string Description => "Live NFL players, 2QB/PPR ADP, and season projections from api.sleeper.app. No API key.";

    public async Task<FantasyDataRefreshResult> RefreshAsync(
        FantasyDataRefreshRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var season = request.Season > 0 ? request.Season : DateTime.UtcNow.Year;
            var now = DateTimeOffset.UtcNow;
            var fetched = new Dictionary<string, SleeperPlayerDto>(StringComparer.OrdinalIgnoreCase);

            foreach (var position in SleeperCatalog.Positions)
            {
                var map = await http.GetFromJsonAsync<Dictionary<string, SleeperPlayerDto>>(
                    $"players/nfl?position={position}&active=true",
                    SleeperCatalog.JsonOptions,
                    cancellationToken);
                if (map is null)
                    continue;
                foreach (var (id, player) in map)
                {
                    if (player is not null)
                        fetched[id] = player;
                }
            }

            if (fetched.Count == 0)
                return FantasyDataRefreshResult.Fail("Sleeper returned no active players.");

            Dictionary<string, SleeperProjectionDto>? projections = null;
            try
            {
                projections = await http.GetFromJsonAsync<Dictionary<string, SleeperProjectionDto>>(
                    $"projections/nfl/regular/{season}",
                    SleeperCatalog.JsonOptions,
                    cancellationToken);
            }
            catch (HttpRequestException)
            {
                projections = null;
            }
            catch (JsonException)
            {
                projections = null;
            }

            var players = new List<Player>();
            var ids = new List<PlayerProviderId>();
            var ranked = new List<(Player Player, string ExternalId, int SearchRank, double? Adp, SleeperProjectionDto? Stats)>();

            foreach (var (externalId, dto) in fetched)
            {
                var player = SleeperCatalog.ToPlayer(dto, now);
                if (player is null)
                    continue;

                SleeperProjectionDto? stats = null;
                projections?.TryGetValue(externalId, out stats);
                var searchRank = dto.SearchRank is > 0 and <= SleeperCatalog.MaxSearchRank
                    ? dto.SearchRank.Value
                    : 10_000;
                ranked.Add((player, externalId, searchRank, SleeperCatalog.ChooseAdp(stats), stats));
            }

            if (ranked.Count == 0)
                return FantasyDataRefreshResult.Fail("Sleeper returned players, but none were fantasy-relevant.");

            var ordered = ranked
                .OrderBy(row => row.Adp ?? 10_000)
                .ThenBy(row => row.SearchRank)
                .ThenBy(row => row.Player.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var rankings = new List<PlayerRanking>();
            var adp = new List<PlayerAdp>();
            var projectionRows = new List<PlayerProjection>();
            var positionRank = new Dictionary<PlayerPosition, int>();

            for (var i = 0; i < ordered.Count; i++)
            {
                var row = ordered[i];
                var overall = i + 1;
                positionRank.TryGetValue(row.Player.PrimaryPosition, out var atPosition);
                atPosition++;
                positionRank[row.Player.PrimaryPosition] = atPosition;

                players.Add(row.Player);
                ids.Add(new PlayerProviderId
                {
                    PlayerId = row.Player.PlayerId,
                    ProviderKey = Key,
                    ExternalId = row.ExternalId
                });
                rankings.Add(new PlayerRanking
                {
                    PlayerId = row.Player.PlayerId,
                    SourceKey = Key,
                    OverallRank = overall,
                    PositionRank = atPosition,
                    Tier = SleeperCatalog.TierFor(overall),
                    CachedAt = now,
                    SourceTimestamp = now
                });
                if (row.Adp is { } adpValue)
                {
                    adp.Add(new PlayerAdp
                    {
                        PlayerId = row.Player.PlayerId,
                        SourceKey = Key,
                        OverallAdp = adpValue,
                        CachedAt = now,
                        SourceTimestamp = now
                    });
                }
                else if (row.SearchRank <= SleeperCatalog.MaxSearchRank)
                {
                    adp.Add(new PlayerAdp
                    {
                        PlayerId = row.Player.PlayerId,
                        SourceKey = Key,
                        OverallAdp = row.SearchRank,
                        CachedAt = now,
                        SourceTimestamp = now
                    });
                }

                if (row.Stats is not null)
                    projectionRows.Add(SleeperCatalog.ToProjection(row.Player.PlayerId, Key, row.Stats, now));
            }

            return await writer.WriteAsync(Key, players, ids, rankings, adp, projectionRows, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return FantasyDataRefreshResult.Fail($"Sleeper refresh failed: {ex.Message}");
        }
    }
}
