using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Providers.FantasyData;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public PersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        collection.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    [Fact]
    public async Task Failed_command_does_not_advance_draft_state()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        var failed = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, PlayerId.New()));
        Assert.False(failed.Succeeded);

        var second = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, PlayerId.New()));
        Assert.False(second.Succeeded);

        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        Assert.Empty(state.ActiveSelections);
        Assert.Equal(1, state.Draft.CurrentStateVersion);
        Assert.Equal(1, state.CurrentOverallPick);
    }

    [Fact]
    public async Task Pick_persists_and_survives_reload()
    {
        var (draftId, player) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        var result = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, player));
        Assert.True(result.Succeeded, result.Error);

        var reloaded = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.ActiveSelections);
        Assert.Equal(player, reloaded.ActiveSelections[1].PlayerId);
        Assert.Equal(2, reloaded.CurrentOverallPick);
        Assert.True(reloaded.UnavailablePlayers.Contains(player));
    }

    [Fact]
    public async Task Rollback_and_rebuild_from_events_restore_availability()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var second = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, second))).Succeeded);
        var rollback = await commands.RollbackAsync(new RollbackDraftCommand(draftId, 1));
        Assert.True(rollback.Succeeded, rollback.Error);

        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        Assert.Single(state.ActiveSelections);
        Assert.False(state.UnavailablePlayers.Contains(second));
    }

    [Fact]
    public async Task Seed_provider_writes_cache()
    {
        var provider = _services.GetRequiredService<IFantasyDataProvider>();
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var result = await provider.RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.PlayersWritten > 50);

        var rankings = await writer.GetRankingsAsync();
        var adp = await writer.GetAdpAsync();
        Assert.NotEmpty(rankings);
        Assert.NotEmpty(adp);
    }

    private async Task<(DraftId DraftId, PlayerId FirstPlayer)> CreateStartedDraftAsync()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);

        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Test League",
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
        return (draft.DraftId, PlayerId.FromName("Bijan Robinson", "ATL", "RB"));
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
}
