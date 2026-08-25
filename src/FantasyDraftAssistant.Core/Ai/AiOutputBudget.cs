namespace FantasyDraftAssistant.Core.Ai;

public static class AiOutputBudget
{
    public static int MaxOutputTokens(string? model, bool fastMode)
    {
        if (UsesHiddenReasoning(model) || UsesAdaptiveThinking(model))
            return fastMode ? 4000 : 8000;
        return fastMode ? 800 : 2500;
    }

    public static int HttpTimeoutSeconds(string? model, bool fastMode)
    {
        if (UsesHiddenReasoning(model) || UsesAdaptiveThinking(model))
            return fastMode ? 120 : 180;
        return fastMode ? 60 : 120;
    }

    public static bool UsesHiddenReasoning(string? model)
    {
        var key = Normalize(model);
        return key.Contains("gpt-5", StringComparison.Ordinal)
            || key.StartsWith("o1", StringComparison.Ordinal)
            || key.StartsWith("o3", StringComparison.Ordinal)
            || key.StartsWith("o4", StringComparison.Ordinal);
    }

    public static bool UsesAdaptiveThinking(string? model)
    {
        var key = Normalize(model);
        return key.Contains("opus-5", StringComparison.Ordinal)
            || key.Contains("sonnet-5", StringComparison.Ordinal)
            || key.Contains("fable-5", StringComparison.Ordinal)
            || key.Contains("opus-4-8", StringComparison.Ordinal);
    }

    public static bool SupportsClaudeEffort(string? model)
    {
        var key = Normalize(model);
        return key.Contains("opus", StringComparison.Ordinal)
            || key.Contains("sonnet", StringComparison.Ordinal);
    }

    public static string Effort(bool fastMode) => fastMode ? "low" : "medium";

    public static string EmptyOrTruncatedNote(string? model, string? stopReason, bool hadText)
    {
        var truncated = stopReason is "length" or "max_tokens";
        if (!hadText)
        {
            return UsesHiddenReasoning(model)
                ? "No visible text came back. This model spent the output budget on hidden reasoning. Try Ask again, or use Deep."
                : "No response text was returned.";
        }

        return truncated ? "\n\n[Cut off at the token limit.]" : "";
    }

    private static string Normalize(string? model) => (model ?? "").Trim().ToLowerInvariant();
}
