using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class MockAiSeatTests
{
    private static Team Team(int seat, MockPersonality? personality = null, string? aiModel = null) => new()
    {
        TeamId = TeamId.New(),
        LeagueId = LeagueId.New(),
        Name = $"Team {seat}",
        DraftPosition = seat,
        PracticePersonality = personality,
        PracticeAiModel = aiModel
    };

    [Fact]
    public void Ai_is_never_dealt_at_random()
    {
        // "Random" in the UI means "draw from DealBag". Keeping Ai out of that bag is
        // the whole mechanism, so this is the test that protects the behaviour.
        Assert.DoesNotContain(MockPersonality.Ai, MockPersonalityCatalog.DealBag);

        for (var seed = 0; seed < 200; seed++)
            Assert.DoesNotContain(MockPersonality.Ai, MockPersonalityCatalog.Deal(12, seed));
    }

    [Fact]
    public void A_hand_set_ai_seat_is_kept_and_carries_its_model_and_strategy()
    {
        var draftId = DraftId.New();
        var branchId = BranchId.New();
        var teams = new List<Team>
        {
            Team(1),
            Team(2, MockPersonality.Ai, "openai:gpt-5.6-luna"),
            Team(3, MockPersonality.ZeroRb)
        };

        var policies = MockPersonalityCatalog.Assign(teams, userTeamId: null, seed: 7, draftId, branchId);

        var ai = policies.Single(policy => policy.Personality == MockPersonality.Ai);
        Assert.Equal("openai:gpt-5.6-luna", ai.AiModel);
        Assert.NotNull(ai.AiStrategy);
        Assert.Equal(MockPersonality.ZeroRb, policies.Single(p => p.TeamId.Equals(teams[2].TeamId)).Personality);
    }

    [Fact]
    public void Only_an_ai_seat_carries_a_model()
    {
        var teams = new List<Team> { Team(1, MockPersonality.ZeroRb, "openai:gpt-5.6-luna") };

        var policy = MockPersonalityCatalog
            .Assign(teams, userTeamId: null, seed: 1, DraftId.New(), BranchId.New())
            .Single();

        Assert.Null(policy.AiModel);
        Assert.Null(policy.AiStrategy);
    }

    [Fact]
    public void A_seats_strategy_is_stable_within_a_branch_and_varies_across_drafts()
    {
        var branchId = BranchId.New();
        var teamId = TeamId.New();
        var first = DraftId.New();

        var a = MockAiStrategyCatalog.ForSeat(first, branchId, teamId);
        var b = MockAiStrategyCatalog.ForSeat(first, branchId, teamId);
        Assert.Equal(a.Key, b.Key);

        // Across many other drafts at least one seat should land somewhere else,
        // otherwise the seed is not doing anything.
        var others = Enumerable.Range(0, 60)
            .Select(_ => MockAiStrategyCatalog.ForSeat(DraftId.New(), branchId, teamId).Key)
            .Distinct()
            .ToList();
        Assert.True(others.Count > 1, "strategy never varied across drafts");
    }

    [Fact]
    public void Every_strategy_has_a_deterministic_proxy_that_is_not_itself_ai()
    {
        // The proxy is what the turn outlook uses. If one were Ai the forecast would
        // recurse straight back into wanting a provider.
        Assert.All(MockAiStrategyCatalog.All, strategy =>
        {
            Assert.NotEqual(MockPersonality.Ai, strategy.Proxy);
            Assert.False(string.IsNullOrWhiteSpace(strategy.PromptLine));
            Assert.False(string.IsNullOrWhiteSpace(strategy.Title));
        });
    }

    [Fact]
    public void An_unknown_strategy_key_falls_back_rather_than_throwing()
    {
        Assert.Equal(MockAiStrategyCatalog.All[0].Key, MockAiStrategyCatalog.Find("no-such-strategy").Key);
        Assert.Equal(MockAiStrategyCatalog.All[0].Key, MockAiStrategyCatalog.Find(null).Key);
    }

    [Fact]
    public void No_strategy_assumes_a_format_the_league_might_not_have()
    {
        // A strategy line that says "especially in Superflex" was enough to make a
        // model take a quarterback second overall in a 1-QB league. Format belongs in
        // the scoring summary, which is built from the actual roster.
        Assert.All(MockAiStrategyCatalog.All, strategy =>
            Assert.DoesNotContain("especially in superflex", strategy.PromptLine, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_personality_catalog_describes_the_ai_seat()
    {
        Assert.Equal("AI drafter", MockPersonalityCatalog.Title(MockPersonality.Ai));
        Assert.False(string.IsNullOrWhiteSpace(MockPersonalityCatalog.Blurb(MockPersonality.Ai)));
    }
}
