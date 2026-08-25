using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class AiCostEstimateTests
{
    [Fact]
    public void Estimates_a_positive_usd_amount()
    {
        var cost = AiCostEstimate.EstimateUsd("grok-4.6", inputChars: 8000, outputChars: 1200);
        Assert.True(cost > 0);
        Assert.StartsWith("~$", AiCostEstimate.Label(cost));
    }

    [Fact]
    public void Zero_label_has_no_tilde()
    {
        Assert.Equal("$0.00", AiCostEstimate.Label(0));
    }

    [Fact]
    public void Value_models_cost_less_than_flagships()
    {
        const int input = 12_000;
        const int output = 2_000;
        Assert.True(
            AiCostEstimate.EstimateUsd("gpt-5.6-luna", input, output)
            < AiCostEstimate.EstimateUsd("gpt-5.6-sol", input, output));
        Assert.True(
            AiCostEstimate.EstimateUsd("claude-haiku-4-5", input, output)
            < AiCostEstimate.EstimateUsd("claude-sonnet-5", input, output));
        Assert.True(
            AiCostEstimate.EstimateUsd("claude-sonnet-5", input, output)
            < AiCostEstimate.EstimateUsd("claude-opus-5", input, output));
        Assert.True(
            AiCostEstimate.EstimateUsd("grok-4.3", input, output)
            < AiCostEstimate.EstimateUsd("grok-4.6", input, output));
    }

    [Fact]
    public void Luna_uses_the_catalog_rate()
    {
        // 12_000 chars ≈ 3_000 tokens in, 2_000 chars ≈ 500 tokens out
        // Luna is $0.20 / $1.20 per 1M → 0.0006 + 0.0006 = 0.0012
        Assert.Equal(0.0012m, AiCostEstimate.EstimateUsd("gpt-5.6-luna", 12_000, 2_000));
    }
}
