using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.AI;

internal static class DraftAnalystPrompt
{
    public static string Build(AiAnalysisRequest request, string decisionContext) =>
        $"""
        You are a fantasy football draft analyst. Use only the provided draft context.
        Be concise. In fast mode, lead with a ranked shortlist and one recommendation.

        Draft state version: {request.StateVersion}
        Fast mode: {request.FastMode}

        Decision context JSON:
        {decisionContext}

        User question:
        {request.Prompt}
        """;
}
