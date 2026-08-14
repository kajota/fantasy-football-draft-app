using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class AiProviderCatalogTests
{
    [Fact]
    public void Catalog_includes_chatgpt_claude_and_grok()
    {
        var keys = AiProviderCatalog.All.Select(p => p.ProviderKey).ToList();
        Assert.Contains(AiProviderCatalog.OpenAi, keys);
        Assert.Contains(AiProviderCatalog.Anthropic, keys);
        Assert.Contains(AiProviderCatalog.Xai, keys);
        Assert.Contains(AiProviderCatalog.All, p => p.ProductName == "ChatGPT");
        Assert.Contains(AiProviderCatalog.All, p => p.ProductName == "Claude");
        Assert.Contains(AiProviderCatalog.All, p => p.ProductName == "Grok");
    }
}
