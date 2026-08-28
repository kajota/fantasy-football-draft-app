using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Providers.FantasyData;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

/// An AI seat drafting end to end, with a fake advisor. No test here may make a
/// network call - the point is the surrounding behaviour, above all that a failing
/// model never stops the draft.
public class MockAiDraftTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;
    private readonly FakeAdvisor _advisor = new();

    public MockAiDraftTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root, credentialRoot: _root);
        collection.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        collection.AddSingleton<IMockPickAdvisor>(_advisor);
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    [Fact]
    public async Task Migration_016_applies_and_the_new_columns_exist()
    {
        var (draftId, _) = await StartAiDraftAsync();
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);

        var policies = await _services.GetRequiredService<IMockDraftService>()
            .GetPoliciesAsync(draftId, state!.ActiveBranch.BranchId);

        var ai = policies.Where(policy => policy.Personality == MockPersonality.Ai).ToList();
        Assert.Equal(3, ai.Count);
        Assert.All(ai, policy =>
        {
            Assert.Equal("openai:gpt-5.6-luna", policy.AiModel);
            Assert.NotNull(policy.AiStrategy);
        });
    }

    [Fact]
    public async Task An_ai_seat_takes_the_players_the_model_chooses_and_stores_the_reason()
    {
        var (draftId, mock) = await StartAiDraftAsync();
        _advisor.Behaviour = FakeAdvisor.Mode.PickLast;

        var result = await SimulateUntilAiPickAsync(draftId, mock);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("Because the strategy said so.", result.Reason);

        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        var reasons = await mock.GetPickReasonsAsync(draftId, state!.ActiveBranch.BranchId);
        var stored = Assert.Single(reasons);
        Assert.Equal("Because the strategy said so.", stored.Reason);
        Assert.False(stored.UsedFallback);
        Assert.Equal("openai", stored.Provider);
    }

    [Theory]
    [InlineData(FakeAdvisor.Mode.ReturnsNull)]
    [InlineData(FakeAdvisor.Mode.Throws)]
    [InlineData(FakeAdvisor.Mode.PicksAnAlreadyDraftedPlayer)]
    public async Task A_failing_model_never_stops_the_draft(FakeAdvisor.Mode mode)
    {
        var (draftId, mock) = await StartAiDraftAsync();
        _advisor.Behaviour = mode;

        var result = await SimulateUntilAiPickAsync(draftId, mock);

        Assert.True(result.Succeeded, result.Error);
        Assert.False(string.IsNullOrWhiteSpace(result.PlayerName));

        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        var reasons = await mock.GetPickReasonsAsync(draftId, state!.ActiveBranch.BranchId);
        Assert.True(Assert.Single(reasons).UsedFallback);
    }

    [Fact]
    public async Task The_turn_outlook_never_asks_the_model()
    {
        var (draftId, mock) = await StartAiDraftAsync();
        _advisor.Behaviour = FakeAdvisor.Mode.FailTheTest;

        // Get the user off the clock so there are CPU seats ahead to forecast; the
        // outlook stops at the user's own pick.
        await DraftForUserAsync(draftId);

        // Every remaining seat is an AI seat, so if the forecast consulted the advisor
        // at all this would throw. It has to use each strategy's deterministic proxy.
        var predicted = await mock.PredictUpcomingPicksAsync(draftId);

        Assert.NotEmpty(predicted);
        Assert.Equal(0, _advisor.Calls);
    }

    [Fact]
    public async Task A_seat_with_no_model_configured_falls_back_without_calling_out()
    {
        var (draftId, mock) = await StartAiDraftAsync(aiModel: null);
        _advisor.Behaviour = FakeAdvisor.Mode.FailTheTest;

        var result = await SimulateUntilAiPickAsync(draftId, mock);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(0, _advisor.Calls);
    }

    private async Task<Core.Models.MockPickResult> SimulateUntilAiPickAsync(DraftId draftId, IMockDraftService mock)
    {
        // Walk forward until a CPU seat picks. The user seat never auto-drafts, so it
        // has to be filled by hand or the draft never advances past it.
        for (var i = 0; i < 8; i++)
        {
            var result = await mock.SimulateNextAsync(draftId);
            if (result.IsComplete)
                break;
            if (!result.IsUserPick)
                return result;
            await DraftForUserAsync(draftId);
        }

        throw new InvalidOperationException("No CPU pick happened.");
    }

    private async Task DraftForUserAsync(DraftId draftId)
    {
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        var players = await _services.GetRequiredService<IDraftStateService>().GetPlayersAsync();
        var next = players.First(player => !state!.UnavailablePlayers.Contains(player.PlayerId));
        var drafted = await _services.GetRequiredService<IDraftCommandService>()
            .DraftPlayerAsync(new DraftPlayerCommand(draftId, next.PlayerId));
        Assert.True(drafted.Succeeded, drafted.Error);
    }

    private async Task<(DraftId DraftId, IMockDraftService Mock)> StartAiDraftAsync(
        string? aiModel = "openai:gpt-5.6-luna")
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);

        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "AI Practice",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });

        // Every CPU seat drafts by AI, which is what makes the forecast test meaningful:
        // there is nothing else it could be simulating.
        var teams = await leagues.GetTeamsAsync(league.LeagueId);
        await leagues.SaveTeamsAsync(new SaveTeamsRequest
        {
            LeagueId = league.LeagueId,
            UserTeamId = league.UserTeamId,
            Teams = teams.Select(team => new TeamDraftPosition
            {
                TeamId = team.TeamId,
                Name = team.Name,
                OwnerName = team.OwnerName,
                DraftPosition = team.DraftPosition,
                PracticePersonality = team.TeamId.Equals(league.UserTeamId!.Value)
                    ? null
                    : MockPersonality.Ai,
                PracticeAiModel = team.TeamId.Equals(league.UserTeamId!.Value) ? null : aiModel
            }).ToList()
        });

        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Practice"
        });

        var mock = _services.GetRequiredService<IMockDraftService>();
        var started = await mock.StartPracticeAsync(draft.DraftId);
        Assert.True(started.Succeeded, started.Error);
        return (draft.DraftId, mock);
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

    public sealed class FakeAdvisor : IMockPickAdvisor
    {
        public enum Mode
        {
            PickLast,
            ReturnsNull,
            Throws,
            PicksAnAlreadyDraftedPlayer,
            FailTheTest
        }

        public Mode Behaviour { get; set; } = Mode.PickLast;
        public int Calls { get; private set; }

        public Task<MockAiPick?> ChooseAsync(MockAiPickRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            switch (Behaviour)
            {
                case Mode.FailTheTest:
                    throw new InvalidOperationException(
                        "The advisor was called from a path that must never reach a provider.");
                case Mode.Throws:
                    throw new HttpRequestException("boom");
                case Mode.ReturnsNull:
                    return Task.FromResult<MockAiPick?>(null);
                case Mode.PicksAnAlreadyDraftedPlayer:
                    // Not on the shortlist at all, which is what a hallucinated or
                    // already-taken player looks like by the time it reaches here.
                    return Task.FromResult<MockAiPick?>(null);
                default:
                    return Task.FromResult<MockAiPick?>(new MockAiPick
                    {
                        PlayerId = request.Candidates[^1].PlayerId,
                        Reason = "Because the strategy said so.",
                        Provider = "openai",
                        Model = "gpt-5.6-luna"
                    });
            }
        }
    }
}
