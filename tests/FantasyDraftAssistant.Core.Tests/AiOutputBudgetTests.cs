using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class AiOutputBudgetTests
{
    [Theory]
    [InlineData("gpt-5", true, 4000)]
    [InlineData("gpt-5", false, 8000)]
    [InlineData("claude-opus-5", true, 4000)]
    [InlineData("claude-opus-5", false, 8000)]
    [InlineData("claude-fable-5", true, 4000)]
    [InlineData("gpt-5.6-luna", true, 4000)]
    [InlineData("gpt-4.1", true, 800)]
    [InlineData("claude-sonnet-4-6", true, 800)]
    [InlineData("claude-haiku-4-5", true, 800)]
    public void Output_budget_covers_hidden_reasoning(string model, bool fast, int expected)
    {
        Assert.Equal(expected, AiOutputBudget.MaxOutputTokens(model, fast));
    }

    [Fact]
    public void Empty_gpt5_explains_hidden_reasoning()
    {
        var note = AiOutputBudget.EmptyOrTruncatedNote("gpt-5", "length", hadText: false);
        Assert.Contains("hidden reasoning", note, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Truncated_visible_text_gets_a_cutoff_note()
    {
        Assert.Contains("Cut off", AiOutputBudget.EmptyOrTruncatedNote("claude-opus-5", "max_tokens", hadText: true));
    }
}
