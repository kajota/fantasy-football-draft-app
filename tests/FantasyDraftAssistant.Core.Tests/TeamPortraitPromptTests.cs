using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Tests;

public class TeamPortraitPromptTests
{
    [Fact]
    public void User_team_is_flattering()
    {
        var prompt = TeamPortraitPrompt.Build("Blue Steel", "You", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("Over-the-top awesome", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Distinct face", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blue Steel", prompt);
        Assert.DoesNotContain("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rival_team_is_a_roast()
    {
        var prompt = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Roast, TeamId.New());
        Assert.Contains("roast portrait", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mike", prompt);
        Assert.Contains("Team 6", prompt);
        Assert.Contains("unique loser", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Different_teams_get_different_looks()
    {
        var a = TeamPortraitPrompt.Build("A", "Ann", TeamPortraitTone.Hero, new TeamId(Guid.Parse("11111111-1111-1111-1111-111111111111")));
        var b = TeamPortraitPrompt.Build("B", "Bob", TeamPortraitTone.Hero, new TeamId(Guid.Parse("22222222-2222-2222-2222-222222222222")));
        Assert.NotEqual(a, b);
        Assert.Contains("Outfit:", a);
        Assert.Contains("Setting:", b);
        Assert.NotEqual(
            TeamPortraitPrompt.Marking("A", "Ann", new TeamId(Guid.Parse("11111111-1111-1111-1111-111111111111"))),
            TeamPortraitPrompt.Marking("B", "Bob", new TeamId(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"))));
    }

    [Fact]
    public void Checked_normal_uses_the_hero_treatment()
    {
        Assert.Equal(TeamPortraitTone.Hero, TeamPortraitPrompt.ToneFor(isUserTeam: false, normalImage: true));
        var prompt = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("Over-the-top awesome", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Portrait_notes_override_the_default_look()
    {
        var prompt = TeamPortraitPrompt.Build(
            "Team 6",
            "Mike",
            TeamPortraitTone.Hero,
            TeamId.New(),
            "Black woman, late 30s, short hair, glasses");
        Assert.Contains("Black woman, late 30s, short hair, glasses", prompt, StringComparison.Ordinal);
        Assert.Contains("Honor that description", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("white man", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Empty_notes_keep_the_original_default_look()
    {
        Assert.False(TeamPortraitPrompt.SuggestsNotAMan("Mike", "Team 6"));
        Assert.True(TeamPortraitPrompt.SuggestsNotAMan("Sarah", "Team 6"));
        var mike = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("white man", mike, StringComparison.OrdinalIgnoreCase);
        var sarah = TeamPortraitPrompt.Build("Team 6", "Sarah", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("woman", sarah, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("white man", sarah, StringComparison.OrdinalIgnoreCase);
        var blank = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Hero, TeamId.New(), "   ");
        Assert.Contains("white man", blank, StringComparison.OrdinalIgnoreCase);
    }
}
