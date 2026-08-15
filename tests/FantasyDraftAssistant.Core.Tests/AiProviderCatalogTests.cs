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

    [Fact]
    public void Claude_suggests_models_beyond_default_sonnet()
    {
        var claude = AiProviderCatalog.Find(AiProviderCatalog.Anthropic);
        Assert.NotNull(claude);
        Assert.Equal("claude-sonnet-4-6", claude.DefaultModel);
        Assert.Contains("claude-sonnet-4-6", claude.SuggestedModels);
        Assert.Contains("claude-sonnet-5", claude.SuggestedModels);
        Assert.Contains("claude-opus-5", claude.SuggestedModels);
        Assert.Contains("claude-haiku-4-5", claude.SuggestedModels);
    }
}
