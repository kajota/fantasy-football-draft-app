namespace FantasyDraftAssistant.Core.Ai;

public static class AiCostEstimate
{
    public static decimal EstimateUsd(string? model, int inputChars, int outputChars)
    {
        var (inputPerMillion, outputPerMillion) = Rates(model);
        var inputTokens = Math.Max(1, inputChars / 4);
        var outputTokens = Math.Max(0, outputChars / 4);
        return Math.Round(
            inputTokens * inputPerMillion / 1_000_000m + outputTokens * outputPerMillion / 1_000_000m,
            4,
            MidpointRounding.AwayFromZero);
    }

    public static string Label(decimal usd) =>
        usd <= 0 ? "$0.00" : $"~${usd:0.00}";

    private static (decimal Input, decimal Output) Rates(string? model)
    {
        var key = (model ?? "").Trim().ToLowerInvariant();
        if (key.Contains("opus", StringComparison.Ordinal))
            return (15.00m, 75.00m);
        if (key.Contains("gpt-4.1", StringComparison.Ordinal))
            return (2.00m, 8.00m);
        if (key.Contains("sonnet", StringComparison.Ordinal) || key.Contains("grok", StringComparison.Ordinal))
            return (3.00m, 15.00m);
        return (3.00m, 15.00m);
    }
}
