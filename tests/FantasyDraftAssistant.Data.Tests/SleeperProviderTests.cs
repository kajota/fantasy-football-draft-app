using System.Net;
using System.Text;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Providers.FantasyData;

namespace FantasyDraftAssistant.Data.Tests;

public class SleeperProviderTests
{
    [Fact]
    public async Task Refresh_maps_players_adp_and_projections()
    {
        var handler = new ScriptedHandler();
        handler.Responses["players/nfl?position=QB&active=true"] = """
            {"4984":{"player_id":"4984","full_name":"Josh Allen","team":"BUF","position":"QB","search_rank":3,"injury_status":null,"status":"Active"}}
            """;
        handler.Responses["players/nfl?position=RB&active=true"] = "{}";
        handler.Responses["players/nfl?position=WR&active=true"] = "{}";
        handler.Responses["players/nfl?position=TE&active=true"] = "{}";
        handler.Responses["players/nfl?position=K&active=true"] = "{}";
        handler.Responses["players/nfl?position=DEF&active=true"] = """
            {"ARI":{"player_id":"ARI","first_name":"Arizona","last_name":"Cardinals","team":"ARI","position":"DEF"}}
            """;
        handler.Responses["projections/nfl/regular/2026"] = """
            {"4984":{"adp_2qb":3.2,"adp_ppr":23.8,"pass_yd":3650,"pass_td":27,"pass_int":10,"pass_att":474,"pass_cmp":313,"rush_yd":535,"rush_td":11,"rush_att":110}}
            """;

        var writer = new CapturingWriter();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.sleeper.app/v1/") };
        var provider = new SleeperFantasyDataProvider(http, writer);

        var result = await provider.RefreshAsync(new FantasyDataRefreshRequest { Season = 2026 }, CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(2, result.PlayersWritten);
        Assert.Equal(2, result.RankingsWritten);
        Assert.Equal(1, result.AdpWritten);
        Assert.Equal(1, result.ProjectionsWritten);
        Assert.Contains(writer.Players, p => p.Name == "Josh Allen" && p.PrimaryPosition == PlayerPosition.QB);
        Assert.Contains(writer.Players, p => p.Name == "Arizona Cardinals" && p.PrimaryPosition == PlayerPosition.DEF);
        Assert.Equal(3.2, writer.Adp.Single(a => a.OverallAdp < 10).OverallAdp);
        Assert.Equal(3650, writer.Projections[0].PassingYards);
        Assert.Equal("4984", writer.Ids.Single(id => id.ExternalId == "4984").ExternalId);
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        public Dictionary<string, string> Responses { get; } = new(StringComparer.OrdinalIgnoreCase);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri is null
                ? ""
                : request.RequestUri.PathAndQuery.TrimStart('/');
            if (path.StartsWith("v1/", StringComparison.OrdinalIgnoreCase))
                path = path[3..];

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
    }
}
