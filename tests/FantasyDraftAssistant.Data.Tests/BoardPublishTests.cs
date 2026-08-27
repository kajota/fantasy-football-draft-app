using System.Net;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Data.Services;
using FantasyDraftAssistant.Providers.FantasyData;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class BoardPublishTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;
    private readonly RecordingHandler _http = new();

    public BoardPublishTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root, credentialRoot: _root);
        // Keep the test off the machine's real keyring and real credential file.
        collection.AddSingleton<ICredentialStore>(sp => sp.GetRequiredService<FileCredentialStore>());
        collection.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        collection.AddSingleton<IBoardPublisher>(sp => new HttpsBoardPublisher(
            new HttpClient(_http) { Timeout = TimeSpan.FromSeconds(5) },
            sp.GetRequiredService<IDraftStateService>(),
            sp.GetRequiredService<IDraftQueryService>(),
            sp.GetRequiredService<IFantasyDataWriter>(),
            sp.GetRequiredService<IAppSettingsStore>(),
            sp.GetRequiredService<ICredentialStore>(),
            sp.GetRequiredService<IDraftChangeNotifier>()));
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    [Fact]
    public async Task Migration_seeds_known_league_slugs()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "FilthyMothers",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4
        });
        // Seed UPDATE only matches rows present at migration time; new leagues stay unset.
        var loaded = await leagues.GetLeagueAsync(league.LeagueId);
        Assert.NotNull(loaded);
        Assert.False(loaded.PublishBoard);
    }

    [Fact]
    public async Task Save_and_reload_board_publish_settings()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Fanatics",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4
        });
        await leagues.SaveBoardPublishAsync(league.LeagueId, "Football Fanatics", true);
        var loaded = await leagues.GetLeagueAsync(league.LeagueId);
        Assert.NotNull(loaded);
        Assert.Equal("football-fanatics", loaded.BoardSlug);
        Assert.True(loaded.PublishBoard);
    }

    [Fact]
    public async Task Invalid_slug_is_rejected()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Bad",
            Season = 2026,
            TeamCount = 2,
            DraftType = DraftType.Snake,
            RoundCount = 2
        });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            leagues.SaveBoardPublishAsync(league.LeagueId, "Nope_Nope", true));
    }

    [Fact]
    public async Task Publish_puts_html_and_json_with_bearer_token()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var leagues = _services.GetRequiredService<ILeagueService>();
        await leagues.SaveBoardPublishAsync(state.League.LeagueId, "filthymothers", true);
        await _services.GetRequiredService<ICredentialStore>()
            .SaveSecretAsync(BoardSlug.CredentialScope, BoardSlug.CredentialKey, "test-token");

        var publisher = _services.GetRequiredService<IBoardPublisher>();
        await publisher.PublishNowAsync(draftId, state.ActiveBranch.BranchId);

        Assert.Equal(5, _http.Calls.Count);
        Assert.All(_http.Calls, call =>
        {
            Assert.Equal(HttpMethod.Put, call.Method);
            Assert.Equal("Bearer test-token", call.Authorization);
        });
        Assert.Equal(
            ["index.html", "more.html", "players.html", "board.json", "available.json"],
            _http.Calls.Select(call => call.Url.Split('/').Last()).ToArray());
        Assert.Contains("more.html", _http.Calls[0].Body, StringComparison.Ordinal);
        Assert.DoesNotContain("__APP_VERSION__", _http.Calls[0].Body, StringComparison.Ordinal);
        Assert.Contains("v", _http.Calls[0].Body, StringComparison.Ordinal);
        Assert.Contains("players.html", _http.Calls[1].Body, StringComparison.Ordinal);
        Assert.Contains("FilthyMothers", _http.Calls[3].Body, StringComparison.Ordinal);
        Assert.Contains("practice", _http.Calls[3].Body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"name\"", _http.Calls[4].Body, StringComparison.Ordinal);
        Assert.Contains("Published", publisher.LastStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Remaining_players_omit_someone_already_drafted()
    {
        var (draftId, bijan) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, bijan))).Succeeded);
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var remaining = await _services.GetRequiredService<IDraftQueryService>()
            .GetRemainingPlayersAsync(new QueryContext
            {
                DraftId = draftId,
                BranchId = state.ActiveBranch.BranchId
            });
        Assert.DoesNotContain(remaining.Players, player => player.Name.Contains("Bijan", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(remaining.Players);
        Assert.False(remaining.Practice);
    }

    [Fact]
    public async Task Publish_is_skipped_when_disabled()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var publisher = _services.GetRequiredService<IBoardPublisher>();
        await publisher.PublishNowAsync(draftId);
        Assert.Empty(_http.Calls);
    }

    [Fact]
    public async Task Http_failure_does_not_throw()
    {
        _http.Status = HttpStatusCode.Forbidden;
        var (draftId, _) = await CreateStartedDraftAsync();
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        await _services.GetRequiredService<ILeagueService>()
            .SaveBoardPublishAsync(state.League.LeagueId, "strata", true);
        await _services.GetRequiredService<ICredentialStore>()
            .SaveSecretAsync(BoardSlug.CredentialScope, BoardSlug.CredentialKey, "test-token");

        var publisher = _services.GetRequiredService<IBoardPublisher>();
        await publisher.PublishNowAsync(draftId);
        Assert.Contains("failed", publisher.LastStatus, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<(DraftId DraftId, PlayerId FirstPlayer)> CreateStartedDraftAsync()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "FilthyMothers",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Test Draft"
        });
        var start = await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new StartDraftCommand(draft.DraftId));
        Assert.True(start.Succeeded, start.Error);
        return (draft.DraftId, PlayerId.FromName("Bijan Robinson", "RB"));
    }

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; } = HttpStatusCode.NoContent;
        public List<(HttpMethod Method, string Url, string? Authorization, string Body)> Calls { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Calls.Add((request.Method, request.RequestUri?.ToString() ?? "", request.Headers.Authorization?.ToString(), body));
            return new HttpResponseMessage(Status);
        }
    }
}
