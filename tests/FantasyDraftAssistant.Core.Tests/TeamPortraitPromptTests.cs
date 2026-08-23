using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Tests;

public class TeamPortraitPromptTests
{
    [Fact]
    public void User_team_is_flattering()
    {
        var prompt = TeamPortraitPrompt.Build("Blue Steel", "You", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("Art style (mandatory", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Distinct face", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Blue Steel", prompt);
        Assert.DoesNotContain("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Square illustrated", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rival_team_is_a_roast()
    {
        var prompt = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Roast, TeamId.New());
        Assert.Contains("Square roast", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Mike", prompt);
        Assert.Contains("Team 6", prompt);
        Assert.Contains("Art style (mandatory", prompt, StringComparison.OrdinalIgnoreCase);
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
            TeamPortraitPrompt.Marking("A", "Ann", 1),
            TeamPortraitPrompt.Marking("B", "Bob", 2));
    }

    [Fact]
    public void Checked_normal_uses_the_hero_treatment()
    {
        Assert.Equal(TeamPortraitTone.Hero, TeamPortraitPrompt.ToneFor(isUserTeam: false, normalImage: true));
        var prompt = TeamPortraitPrompt.Build("Team 6", "Mike", TeamPortraitTone.Hero, TeamId.New());
        Assert.Contains("Art style (mandatory", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("terrible at fantasy", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Square roast", prompt, StringComparison.OrdinalIgnoreCase);
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

    [Fact]
    public void New_spin_changes_outfit_and_style_not_the_person()
    {
        var id = new TeamId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var a = TeamPortraitPrompt.Build("Blue Steel", "Mike", TeamPortraitTone.Hero, id, spin: 1);
        var b = TeamPortraitPrompt.Build("Blue Steel", "Mike", TeamPortraitTone.Hero, id, spin: 99);
        Assert.NotEqual(a, b);
        Assert.Contains("white man", a, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("white man", b, StringComparison.OrdinalIgnoreCase);
        var outfitA = a.Split("Outfit:")[1].Split('.')[0];
        var outfitB = b.Split("Outfit:")[1].Split('.')[0];
        var styleA = a.Split("Art style (mandatory, every pixel):")[1].Split('\n')[0];
        var styleB = b.Split("Art style (mandatory, every pixel):")[1].Split('\n')[0];
        Assert.True(outfitA != outfitB || styleA != styleB);
    }

    [Fact]
    public void Picked_art_style_is_in_the_prompt()
    {
        var prompt = TeamPortraitPrompt.Build(
            "Blue Steel",
            "Mike",
            TeamPortraitTone.Roast,
            TeamId.New(),
            artStyleKey: "photoreal");
        Assert.StartsWith("Art style (mandatory, every pixel): photoreal cinematic still", prompt.Trim(), StringComparison.Ordinal);
        Assert.DoesNotContain("supermarket flyer", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Not photoreal", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Square illustrated", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Random_style_can_be_photoreal_not_only_illustration()
    {
        var phrases = new HashSet<string>();
        var id = new TeamId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        for (var spin = 0; spin < 40; spin++)
        {
            var prompt = TeamPortraitPrompt.Build("Blue Steel", "Mike", TeamPortraitTone.Hero, id, spin: spin);
            var line = prompt.Split('\n')[0];
            phrases.Add(line);
        }

        Assert.Contains(phrases, line => line.Contains("photoreal", StringComparison.OrdinalIgnoreCase));
        Assert.True(phrases.Count > 4);
    }

    [Fact]
    public void Editorial_cartoon_locks_ink_and_bans_neon_cinema()
    {
        var prompt = TeamPortraitPrompt.Build(
            "Devil Biscuits",
            "You",
            TeamPortraitTone.Hero,
            TeamId.New(),
            artStyleKey: "cartoon");
        Assert.Contains("newspaper editorial cartoon", prompt, StringComparison.Ordinal);
        Assert.Contains("Forbidden: painterly digital painting", prompt, StringComparison.Ordinal);
        Assert.DoesNotContain("neon night market", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("vintage convertible", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
