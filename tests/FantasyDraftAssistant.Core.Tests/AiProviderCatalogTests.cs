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
    public void Defaults_are_the_value_picks_not_flagships()
    {
        Assert.Equal("gpt-5.6-luna", AiProviderCatalog.Find(AiProviderCatalog.OpenAi)?.DefaultModel);
        Assert.Equal("claude-sonnet-5", AiProviderCatalog.Find(AiProviderCatalog.Anthropic)?.DefaultModel);
        Assert.Equal("grok-4.3", AiProviderCatalog.Find(AiProviderCatalog.Xai)?.DefaultModel);
    }

    [Fact]
    public void Dropdowns_list_cheap_and_flagship_models()
    {
        var chatgpt = AiProviderCatalog.Find(AiProviderCatalog.OpenAi);
        Assert.NotNull(chatgpt);
        Assert.Contains("gpt-5.6-luna", chatgpt.SuggestedModelIds);
        Assert.Contains("gpt-4.1-mini", chatgpt.SuggestedModelIds);
        Assert.Contains("gpt-5.6-sol", chatgpt.SuggestedModelIds);

        var claude = AiProviderCatalog.Find(AiProviderCatalog.Anthropic);
        Assert.NotNull(claude);
        Assert.Contains("claude-haiku-4-5", claude.SuggestedModelIds);
        Assert.Contains("claude-sonnet-5", claude.SuggestedModelIds);
        Assert.Contains("claude-opus-5", claude.SuggestedModelIds);
        Assert.Contains("claude-fable-5", claude.SuggestedModelIds);

        var grok = AiProviderCatalog.Find(AiProviderCatalog.Xai);
        Assert.NotNull(grok);
        Assert.Contains("grok-4.3", grok.SuggestedModelIds);
        Assert.Contains("grok-build-0.1", grok.SuggestedModelIds);
        Assert.Contains("grok-4.6", grok.SuggestedModelIds);
    }

    [Fact]
    public void Display_names_include_the_api_id_and_a_price()
    {
        var luna = AiProviderCatalog.FindModel("gpt-5.6-luna");
        Assert.NotNull(luna);
        Assert.Contains("gpt-5.6-luna", luna.DisplayName, StringComparison.Ordinal);
        Assert.Contains("$0.20", luna.DisplayName, StringComparison.Ordinal);
        Assert.Contains("best value", luna.DisplayName, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FindModel_matches_dated_snapshot_ids()
    {
        var haiku = AiProviderCatalog.FindModel("claude-haiku-4-5-20251001");
        Assert.NotNull(haiku);
        Assert.Equal("claude-haiku-4-5", haiku.Id);
    }
}
