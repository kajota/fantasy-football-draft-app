using FantasyDraftAssistant.Providers.AI;

namespace FantasyDraftAssistant.Data.Tests;

public class TeamPortraitGeneratorTests
{
    [Fact]
    public void Chatgpt_portraits_pin_medium_quality()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            TeamPortraitGenerator.OpenAiImageBody("a team portrait"));
        Assert.Contains("\"quality\":\"medium\"", json);
        Assert.DoesNotContain("\"quality\":\"auto\"", json);
        Assert.DoesNotContain("\"quality\":\"high\"", json);
        Assert.Contains("\"size\":\"1024x1024\"", json);
        Assert.Contains("\"model\":\"gpt-image-1\"", json);
    }

    [Fact]
    public void Grok_portraits_already_pin_medium_quality()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(
            TeamPortraitGenerator.XaiImageBody("a team portrait"));
        Assert.Contains("\"quality\":\"medium\"", json);
        Assert.Contains("\"model\":\"grok-imagine-image-2.0\"", json);
    }
}
