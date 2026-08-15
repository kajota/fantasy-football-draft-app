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
}
