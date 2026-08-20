using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Providers.FantasyData;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class DraftForecastTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public DraftForecastTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        collection.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private async Task<(DraftId Draft, TeamId User)> StartedDraftAsync()
    {
        await _services.GetRequiredService<IFantasyDataProvider>()
            .RefreshAsync(new Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);

        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Forecast League",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Forecast Draft"
        });
        Assert.True((await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new StartDraftCommand(draft.DraftId))).Succeeded);

        var updated = await leagues.GetLeagueAsync(league.LeagueId);
        return (draft.DraftId, updated!.UserTeamId!.Value);
    }

    [Fact]
    public async Task Nothing_is_forecast_while_the_user_is_on_the_clock()
    {
        // The user drafts first here, so there is no one to predict yet.
        var (draftId, _) = await StartedDraftAsync();

        Assert.Empty(await _services.GetRequiredService<IMockDraftService>()
            .PredictUpcomingPicksAsync(draftId));
    }

    [Fact]
    public async Task Forecast_covers_every_seat_up_to_the_users_next_pick()
    {
        var (draftId, userTeam) = await StartedDraftAsync();
        Assert.True((await _services.GetRequiredService<IDraftCommandService>()
            .DraftPlayerAsync(new DraftPlayerCommand(draftId, PlayerId.FromName("Bijan Robinson", "RB")))).Succeeded);

        var predicted = await _services.GetRequiredService<IMockDraftService>()
            .PredictUpcomingPicksAsync(draftId);

        // Four-team snake: the user took 1, picks again at 8, so 2-7 get predicted.
        Assert.Equal([2, 3, 4, 5, 6, 7], predicted.Select(pick => pick.OverallPick));
        Assert.DoesNotContain(predicted, pick => pick.TeamId.Equals(userTeam));
        Assert.All(predicted, pick => Assert.False(string.IsNullOrWhiteSpace(pick.PlayerName)));
        Assert.All(predicted, pick => Assert.Matches(@"^\d+\.\d\d$", pick.RoundPick));
    }

    [Fact]
    public async Task Forecast_never_predicts_the_same_player_twice()
    {
        var (draftId, _) = await StartedDraftAsync();
        var predicted = await _services.GetRequiredService<IMockDraftService>()
            .PredictUpcomingPicksAsync(draftId);

        Assert.Equal(predicted.Select(p => p.PlayerId).Distinct().Count(), predicted.Count);
    }

    [Fact]
    public async Task Forecast_changes_nothing_on_the_board()
    {
        var (draftId, _) = await StartedDraftAsync();
        var states = _services.GetRequiredService<IDraftStateService>();
        var before = await states.GetWorkingStateAsync(draftId);
        var versionBefore = before!.Draft.CurrentStateVersion;

        await _services.GetRequiredService<IMockDraftService>().PredictUpcomingPicksAsync(draftId);

        var after = await states.GetWorkingStateAsync(draftId);
        Assert.Empty(after!.ActiveSelections);
        Assert.Equal(versionBefore, after.Draft.CurrentStateVersion);
    }

    [Fact]
    public async Task Forecast_avoids_players_already_drafted()
    {
        var (draftId, _) = await StartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var taken = PlayerId.FromName("Bijan Robinson", "RB");
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, taken))).Succeeded);

        var predicted = await _services.GetRequiredService<IMockDraftService>()
            .PredictUpcomingPicksAsync(draftId);

        Assert.DoesNotContain(predicted, pick => pick.PlayerId.Equals(taken));
    }
}
