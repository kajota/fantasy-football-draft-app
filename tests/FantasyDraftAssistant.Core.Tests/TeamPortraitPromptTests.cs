using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Tests;

public class TeamPortraitPromptTests
{
    [Fact]
    public void User_team_is_flattering()
    {
        var prompt = TeamPortraitPrompt.Build("Blue Steel", "You", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("handsome", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Over-the-top awesome", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blue Steel", prompt);
        Assert.DoesNotContain("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rival_team_is_a_roast()
    {
        var prompt = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Roast, TeamId.New());
        Assert.Contains("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mike", prompt);
        Assert.Contains("Team 6", prompt);
    }

    [Fact]
    public void Checked_normal_uses_the_hero_treatment()
    {
        Assert.Equal(TeamPortraitTone.Hero, TeamPortraitPrompt.ToneFor(isUserTeam: false, normalImage: true));
        var prompt = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("Over-the-top awesome", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
