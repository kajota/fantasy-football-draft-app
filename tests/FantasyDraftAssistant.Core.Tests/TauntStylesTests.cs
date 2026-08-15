using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class TauntStylesTests
{
    [Fact]
    public void All_three_providers_get_distinct_house_styles()
    {
        var map = TauntStyles.Assign([AiProviderCatalog.OpenAi, AiProviderCatalog.Anthropic, AiProviderCatalog.Xai]);
        Assert.Equal(TauntStyles.Melville, map[AiProviderCatalog.Anthropic]);
        Assert.Equal(TauntStyles.Kayfabe, map[AiProviderCatalog.OpenAi]);
        Assert.Equal(TauntStyles.LockerRoom, map[AiProviderCatalog.Xai]);
    }

    [Fact]
    public void Grok_alone_stays_vulgar()
    {
        var map = TauntStyles.Assign([AiProviderCatalog.Xai]);
        Assert.Equal(TauntStyles.LockerRoom, map[AiProviderCatalog.Xai]);
    }

    [Fact]
    public void Without_claude_chatgpt_gets_melville()
    {
        var map = TauntStyles.Assign([AiProviderCatalog.OpenAi, AiProviderCatalog.Xai]);
        Assert.Equal(TauntStyles.Melville, map[AiProviderCatalog.OpenAi]);
        Assert.Equal(TauntStyles.LockerRoom, map[AiProviderCatalog.Xai]);
    }
}
