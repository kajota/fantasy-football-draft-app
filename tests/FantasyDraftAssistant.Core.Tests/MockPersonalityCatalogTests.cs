using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Tests;

public class MockPersonalityCatalogTests
{
    private static readonly LeagueId League = LeagueId.New();

    private static Team Team(string name, int seat, MockPersonality? personality = null) => new()
    {
        TeamId = TeamId.New(),
        LeagueId = League,
        Name = name,
        DraftPosition = seat,
        PracticePersonality = personality
    };

    [Fact]
    public void Saved_practice_personalities_are_honored()
    {
        var zero = Team("Zero Fan", 2, MockPersonality.ZeroRb);
        var rookie = Team("Rookie Fan", 3, MockPersonality.RookieHunter);
        var user = Team("Me", 1);
        var random = Team("Random Seat", 4);
        var teams = new[] { user, zero, rookie, random };

        var policies = MockPersonalityCatalog.Assign(teams, user.TeamId, seed: 42);

        Assert.Equal(MockPersonality.ZeroRb, policies.Single(p => p.TeamId.Equals(zero.TeamId)).Personality);
        Assert.Equal(MockPersonality.RookieHunter, policies.Single(p => p.TeamId.Equals(rookie.TeamId)).Personality);
        Assert.False(policies.Single(p => p.TeamId.Equals(user.TeamId)).IsCpu);
        Assert.True(policies.Single(p => p.TeamId.Equals(random.TeamId)).IsCpu);
    }

    [Fact]
    public void Unset_seats_still_draw_from_the_random_deal()
    {
        var user = Team("Me", 1);
        var teams = new[] { user, Team("A", 2), Team("B", 3), Team("C", 4) };

        var first = MockPersonalityCatalog.Assign(teams, user.TeamId, seed: 7);
        var second = MockPersonalityCatalog.Assign(teams, user.TeamId, seed: 7);

        // Deterministic for a given seed, and every CPU seat has a personality.
        Assert.Equal(
            first.Select(p => p.Personality).ToList(),
            second.Select(p => p.Personality).ToList());
        Assert.Equal(3, first.Count(p => p.IsCpu));
    }

    [Fact]
    public void All_personalities_are_offered_with_titles()
    {
        foreach (var personality in Enum.GetValues<MockPersonality>())
        {
            Assert.False(string.IsNullOrWhiteSpace(MockPersonalityCatalog.Title(personality)));
        }
    }
}
