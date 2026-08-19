using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class DecisionContextTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public DecisionContextTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        collection.AddSingleton<IFantasyDataProvider, FantasyDraftAssistant.Providers.FantasyData.SeedFantasyDataProvider>();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Recent_picks_use_player_names_in_chronological_order()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);
        var second = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, second))).Succeeded);

        var context = await GetContextAsync(draftId);
        Assert.Equal(2, context.RecentPicks.Count);
        Assert.Equal("Bijan Robinson", context.RecentPicks[0].Player);
        Assert.Equal(1, context.RecentPicks[0].OverallPick);
        Assert.Equal("Saquon Barkley", context.RecentPicks[1].Player);
        Assert.Equal(2, context.RecentPicks[1].OverallPick);
    }

    [Fact]
    public async Task Upcoming_picks_follow_true_draft_order_and_flag_the_user()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);

        var context = await GetContextAsync(draftId);
        Assert.Equal(2, context.UpcomingPicks[0].OverallPick);
        Assert.Equal(
            context.UpcomingPicks.Select(p => p.OverallPick).OrderBy(p => p).ToList(),
            context.UpcomingPicks.Select(p => p.OverallPick).ToList());

        // 4-team snake: the user (seat 1) owns picks 1, 8, 9, and 16.
        var userPick = context.UpcomingPicks.Single(p => p.OverallPick == 8);
        Assert.True(userPick.IsUser);
        Assert.Equal(new[] { 8, 9, 16 }, context.MyUpcomingPicks.Select(p => p.OverallPick).ToList());
        Assert.All(context.MyUpcomingPicks, p => Assert.True(p.IsUser));
        Assert.Equal(8, context.Status.UserNextOverallPick);
    }

    [Fact]
    public async Task Intervening_teams_are_empty_while_the_user_is_on_the_clock()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var context = await GetContextAsync(draftId);
        Assert.Empty(context.InterveningTeams);
    }

    [Fact]
    public async Task Intervening_teams_stop_before_the_users_next_pick()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);

        var context = await GetContextAsync(draftId);

        // Picks 2-7 belong to the three other teams, twice each, before pick 8.
        Assert.Equal(3, context.InterveningTeams.Count);
        Assert.All(context.InterveningTeams, team => Assert.Equal(2, team.PicksBeforeUser));
        Assert.DoesNotContain(context.InterveningTeams, team => team.TeamName == context.MyRoster.TeamName);
        Assert.All(context.InterveningTeams, team => Assert.NotNull(team.Roster));
    }

    [Fact]
    public async Task Current_team_roster_tracks_the_team_on_the_clock()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);

        var context = await GetContextAsync(draftId);
        Assert.NotNull(context.CurrentTeamRoster);
        Assert.Equal(context.Status.CurrentTeam, context.CurrentTeamRoster.TeamName);
        Assert.NotEqual(context.MyRoster.TeamName, context.CurrentTeamRoster.TeamName);
    }

    [Fact]
    public async Task Data_freshness_lists_seed_refresh_with_age()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var context = await GetContextAsync(draftId);
        Assert.NotNull(context.DataFreshness);
        Assert.NotEmpty(context.DataFreshness.Sources);
        Assert.All(context.DataFreshness.Sources, source =>
        {
            Assert.False(string.IsNullOrWhiteSpace(source.Age));
            Assert.EndsWith("UTC", source.RefreshedAt);
        });
        Assert.NotNull(context.GeneratedAt);
    }

    [Fact]
    public async Task Available_players_carry_outlook_and_replacement_value()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);

        var context = await GetContextAsync(draftId);
        var withAdp = context.TopAvailable.Where(p => p.OverallAdp is not null).ToList();
        Assert.NotEmpty(withAdp);
        Assert.All(withAdp, p => Assert.NotNull(p.NextPickOutlook));
        Assert.Contains(context.TopAvailable, p => p.PointsAboveReplacement is not null);
        Assert.NotEmpty(context.TierCliffs);
    }

    [Fact]
    public async Task Rookies_are_not_limited_to_the_top_of_the_board()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var context = await GetContextAsync(draftId);
        Assert.All(context.AvailableRookies, p => Assert.True(p.IsRookie));
    }

    [Fact]
    public async Task Keeper_note_is_absent_in_leagues_without_keepers()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var context = await GetContextAsync(draftId);
        Assert.Null(context.League.KeeperNote);
    }

    [Fact]
    public async Task Keeper_note_appears_when_keepers_are_on_the_board()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new FantasyDataRefreshRequest(), CancellationToken.None);
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Keeper League",
            Season = 2026,
            TeamCount = 4,
            DraftType = Core.Enums.DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Keeper Draft"
        });
        var teams = await leagues.GetTeamsAsync(league.LeagueId);
        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draft.DraftId,
            Keepers =
            [
                new KeeperSpec
                {
                    TeamId = teams[0].TeamId,
                    PlayerId = PlayerId.FromName("Bijan Robinson", "ATL", "RB"),
                    RoundCost = 4
                }
            ]
        });
        var start = await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new StartDraftCommand(draft.DraftId));
        Assert.True(start.Succeeded, start.Error);

        var context = await GetContextAsync(draft.DraftId);
        Assert.NotNull(context.League.KeeperNote);
        Assert.Contains("Keeper league", context.League.KeeperNote, StringComparison.Ordinal);
        Assert.Contains("max 1 per team", context.League.KeeperNote, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Prompt_kind_round_trips_through_the_response_store()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var store = _services.GetRequiredService<IAiResponseStore>();

        AiSavedResponse Response(string body, string? kind) => new()
        {
            ResponseId = Guid.NewGuid().ToString("D"),
            DraftId = draftId,
            BranchId = state.ActiveBranch.BranchId,
            Provider = "xai",
            Model = "grok-4.6",
            AnalyzedStateVersion = 1,
            Prompt = "Who should I take?",
            Body = body,
            RequestStartedAt = DateTimeOffset.UtcNow,
            PromptKind = kind
        };

        await store.SaveAsync(Response("Recommendation: Bijan Robinson", null));
        await store.SaveAsync(Response("Your roster is a cautionary tale.", "taunt"));

        var saved = await store.ListAsync(draftId, state.ActiveBranch.BranchId);
        Assert.Equal(2, saved.Count);
        Assert.Contains(saved, r => r.PromptKind is null && r.Body.StartsWith("Recommendation", StringComparison.Ordinal));
        Assert.Contains(saved, r => r.PromptKind == "taunt");

        var window = Core.Ai.ConversationWindow.Select(saved, "xai");
        var turn = Assert.Single(window);
        Assert.Equal("Recommendation: Bijan Robinson", turn.Answer);
    }

    private async Task<DecisionContextDto> GetContextAsync(DraftId draftId)
    {
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        return await _services.GetRequiredService<IDraftQueryService>().GetDecisionContextAsync(
            new QueryContext { DraftId = draftId, BranchId = state.ActiveBranch.BranchId });
    }

    private async Task<(DraftId DraftId, PlayerId FirstPlayer)> CreateStartedDraftAsync()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new FantasyDataRefreshRequest(), CancellationToken.None);

        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Context League",
            Season = 2026,
            TeamCount = 4,
            DraftType = Core.Enums.DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Context Draft"
        });
        var start = await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new StartDraftCommand(draft.DraftId));
        Assert.True(start.Succeeded, start.Error);
        return (draft.DraftId, PlayerId.FromName("Bijan Robinson", "ATL", "RB"));
    }
}
