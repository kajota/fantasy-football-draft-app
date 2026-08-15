using FantasyDraftAssistant.Core.Ai;

namespace FantasyDraftAssistant.Core.Tests;

public class AiAnalysisModeTests
{
    [Fact]
    public void Ask_deep_forces_deep_for_every_role()
    {
        Assert.True(AiAnalysisMode.IsDeep(askDeep: true, AiAnalysisMode.FastAdvisor));
        Assert.True(AiAnalysisMode.IsDeep(askDeep: true, AiAnalysisMode.DeepAdvisor));
        Assert.True(AiAnalysisMode.IsDeep(askDeep: true, "Secondary Opinion"));
    }

    [Fact]
    public void Fast_ask_still_uses_deep_when_the_provider_role_is_deep_advisor()
    {
        Assert.False(AiAnalysisMode.IsDeep(askDeep: false, AiAnalysisMode.FastAdvisor));
        Assert.True(AiAnalysisMode.IsDeep(askDeep: false, AiAnalysisMode.DeepAdvisor));
        Assert.False(AiAnalysisMode.IsDeep(askDeep: false, null));
    }
}
