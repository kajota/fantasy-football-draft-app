using System.Net;
using System.Text;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Providers.FantasyData;

namespace FantasyDraftAssistant.Data.Tests;

public class FantasyProsProviderTests
{
    [Fact]
    public async Task Refresh_requires_a_saved_key()
    {
        var provider = new FantasyProsFantasyDataProvider(
            new HttpClient(new ScriptedHandler()) { BaseAddress = new Uri("https://api.fantasypros.com/public/v2/json/") },
            new CapturingWriter(),
            new MemoryCredentials(),
            TimeSpan.Zero);

        var result = await provider.RefreshAsync(new FantasyDataRefreshRequest { Season = 2026 }, CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Contains("API key", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Refresh_maps_half_ppr_ranks_and_yahoo_ids()
    {
        var handler = new ScriptedHandler();
        handler.Responses["nfl/2026/consensus-rankings?type=draft&scoring=HALF&position=ALL"] = """
            {
              "players": [
                {
                  "player_id": 17298,
                  "player_name": "Bijan Robinson",
                  "player_team_id": "ATL",
                  "player_position_id": "RB",
                  "rank_ecr": 1,
                  "pos_rank": "RB1",
                  "tier": 1,
                  "rank_min": "1",
                  "rank_max": "4",
                  "rank_std": "0.8",
                  "adp": 1.3
                }
              ]
            }
            """;
        handler.Responses["nfl/players"] = """
            { "players": [ { "player_id": 17298, "yahoo_id": "31002", "injury_status": "Questionable", "bye_week": 7 } ] }
            """;
        handler.Responses["nfl/2026/consensus-rankings?type=ADP&scoring=HALF&position=ALL"] = """
            {
              "type": "ADP Half PPR",
              "players": [
                { "player_id": 17298, "player_name": "Bijan Robinson", "player_team_id": "ATL", "player_position_id": "RB", "rank_ecr": 2, "rank_ave": "1.8" }
              ]
            }
            """;
        handler.Responses["nfl/2026/projections?week=0&position=ALL"] = """
            { "players": [ { "player_id": 17298, "rush_yd": 1400, "rush_td": 12, "rec": 70, "rec_yds": 560 } ] }
            """;

        var writer = new CapturingWriter();
        var credentials = new MemoryCredentials();
        await credentials.SaveSecretAsync("fantasydata", "fantasypros", "test-key");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.fantasypros.com/public/v2/json/") };
        var provider = new FantasyProsFantasyDataProvider(http, writer, credentials, TimeSpan.Zero);

        var result = await provider.RefreshAsync(new FantasyDataRefreshRequest { Season = 2026 }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, result.PlayersWritten);
        var player = Assert.Single(writer.Players);
        Assert.Equal("Bijan Robinson", player.Name);
        Assert.Equal(PlayerStatus.Questionable, player.Status);
        Assert.Equal(7, player.ByeWeek);
        Assert.Equal(1, writer.Rankings[0].OverallRank);
        Assert.Equal(1, writer.Rankings[0].RankMin);
        Assert.Equal(4, writer.Rankings[0].RankMax);
        Assert.Equal(0.8, writer.Rankings[0].RankStd);
        Assert.Contains(writer.Rankings, r => r.SourceKey == "fantasypros-half");
        Assert.Contains(writer.Rankings, r => r.SourceKey == "fantasypros");
        Assert.Equal(1.8, writer.Adp[0].OverallAdp);
        Assert.Contains(writer.Adp, row => row.SourceKey == "fantasypros-half");
        Assert.Equal(2, writer.Projections.Count);
        Assert.All(writer.Projections, row => Assert.Equal(1400, row.RushingYards));
        Assert.Contains(writer.Projections, row => row.SourceKey == "fantasypros-half");
        Assert.Contains(writer.Projections, row => row.SourceKey == "fantasypros");
        Assert.Contains(writer.Ids, id => id.ProviderKey == "fantasypros" && id.ExternalId == "17298");
        Assert.Contains(writer.Ids, id => id.ProviderKey == "yahoo" && id.ExternalId == "31002");
        Assert.Equal(4, handler.ApiKeys.Count);
        Assert.All(handler.ApiKeys, key => Assert.Equal("test-key", key));
    }

    /// <summary>
    /// Without an explicit position the endpoint answers with running backs only, and says
    /// nothing about it — which silently left every other position with no projected points.
    /// </summary>
    [Fact]
    public void Projections_are_requested_for_every_position()
    {
        var path = FantasyProsFantasyDataProvider.ProjectionsPath(2026);

        Assert.Contains("position=ALL", path, StringComparison.Ordinal);
        Assert.StartsWith("nfl/2026/projections?", path, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Projections_are_mapped_for_positions_other_than_running_back()
    {
        var handler = new ScriptedHandler();
        handler.Responses["nfl/2026/consensus-rankings?type=draft&scoring=HALF&position=ALL"] = """
            {
              "players": [
                { "player_id": 1, "player_name": "Ja'Marr Chase", "player_team_id": "CIN", "player_position_id": "WR", "rank_ecr": 1 },
                { "player_id": 2, "player_name": "Josh Allen", "player_team_id": "BUF", "player_position_id": "QB", "rank_ecr": 2 },
                { "player_id": 3, "player_name": "Brock Bowers", "player_team_id": "LV", "player_position_id": "TE", "rank_ecr": 3 }
              ]
            }
            """;
        handler.Responses["nfl/2026/projections?week=0&position=ALL"] = """
            {
              "players": [
                { "player_id": 1, "rec": 100, "rec_yds": 1400 },
                { "player_id": 2, "pass_yds": 4100, "pass_td": 30 },
                { "player_id": 3, "rec": 85, "rec_yds": 1100 }
              ]
            }
            """;

        var writer = new CapturingWriter();
        var credentials = new MemoryCredentials();
        await credentials.SaveSecretAsync("fantasydata", "fantasypros", "test-key");
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.fantasypros.com/public/v2/json/") };
        var provider = new FantasyProsFantasyDataProvider(http, writer, credentials, TimeSpan.Zero);

        var result = await provider.RefreshAsync(new FantasyDataRefreshRequest { Season = 2026 }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        var byPlayer = writer.Players.ToDictionary(p => p.PlayerId, p => p.PrimaryPosition);
        var positions = writer.Projections
            .Select(row => byPlayer[row.PlayerId])
            .Distinct()
            .ToList();

        Assert.Contains(PlayerPosition.WR, positions);
        Assert.Contains(PlayerPosition.QB, positions);
        Assert.Contains(PlayerPosition.TE, positions);
        Assert.Contains(writer.Projections, row => row.PassingYards == 4100);
        Assert.Contains(writer.Projections, row => row.Receptions == 100);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<string> ApiKeys { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Headers.TryGetValues("x-api-key", out var keys))
                ApiKeys.AddRange(keys);

            var path = request.RequestUri is null ? "" : request.RequestUri.PathAndQuery.TrimStart('/');
            foreach (var prefix in new[] { "public/v2/json/", "v2/json/" })
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    path = path[prefix.Length..];
            }

            if (!Responses.TryGetValue(path, out var body))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)
                {
                    Content = new StringContent($"{{\"error\":\"missing {path}\"}}", Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class MemoryCredentials : ICredentialStore
    {
        private readonly Dictionary<string, string> _secrets = new(StringComparer.OrdinalIgnoreCase);
        public bool IsSecure => true;
        public string Description => "test";

        public Task SaveSecretAsync(string scope, string key, string secret, CancellationToken cancellationToken = default)
        {
            _secrets[$"{scope}:{key}"] = secret;
            return Task.CompletedTask;
        }

        public Task<string?> GetSecretAsync(string scope, string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_secrets.TryGetValue($"{scope}:{key}", out var secret) ? secret : null);

        public Task DeleteSecretAsync(string scope, string key, CancellationToken cancellationToken = default)
        {
            _secrets.Remove($"{scope}:{key}");
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingWriter : IFantasyDataWriter
    {
        public List<Player> Players { get; } = [];
        public List<PlayerProviderId> Ids { get; } = [];
        public List<PlayerRanking> Rankings { get; } = [];
        public List<PlayerAdp> Adp { get; } = [];
        public List<PlayerProjection> Projections { get; } = [];

        public Task<FantasyDataRefreshResult> WriteAsync(
            string providerKey,
            IReadOnlyList<Player> players,
            IReadOnlyList<PlayerProviderId> providerIds,
            IReadOnlyList<PlayerRanking> rankings,
            IReadOnlyList<PlayerAdp> adp,
            IReadOnlyList<PlayerProjection> projections,
            CancellationToken cancellationToken = default)
        {
            Players.AddRange(players);
            Ids.AddRange(providerIds);
            Rankings.AddRange(rankings);
            Adp.AddRange(adp);
            Projections.AddRange(projections);
            return Task.FromResult(new FantasyDataRefreshResult
            {
                Succeeded = true,
                PlayersWritten = players.Count,
                RankingsWritten = rankings.Count,
                AdpWritten = adp.Count,
                ProjectionsWritten = projections.Count
            });
        }

        public Task<IReadOnlyDictionary<PlayerId, PlayerRanking>> GetRankingsAsync(string? sourceKey = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<PlayerId, PlayerRanking>>(new Dictionary<PlayerId, PlayerRanking>());

        public Task<IReadOnlyDictionary<PlayerId, PlayerAdp>> GetAdpAsync(string? sourceKey = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<PlayerId, PlayerAdp>>(new Dictionary<PlayerId, PlayerAdp>());

        public Task<IReadOnlyDictionary<PlayerId, PlayerProjection>> GetProjectionsAsync(string? sourceKey = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<PlayerId, PlayerProjection>>(new Dictionary<PlayerId, PlayerProjection>());

        public Task<IReadOnlyList<FantasyDataRefreshInfo>> GetRefreshInfoAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FantasyDataRefreshInfo>>([]);

        public Task<IReadOnlyList<string>> GetSourceKeysAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<string>>([]);

        public Task<IReadOnlyDictionary<PlayerId, IReadOnlyDictionary<string, string>>> GetProviderIdsAsync(
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<PlayerId, IReadOnlyDictionary<string, string>>>(
                new Dictionary<PlayerId, IReadOnlyDictionary<string, string>>());
    }
}
