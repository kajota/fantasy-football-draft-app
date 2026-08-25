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
        var listed = AiProviderCatalog.FindModel(model);
        if (listed is not null)
            return (listed.InputPerMillion, listed.OutputPerMillion);

        var key = (model ?? "").Trim().ToLowerInvariant();
        if (key.Contains("fable", StringComparison.Ordinal) || key.Contains("mythos", StringComparison.Ordinal))
            return (10.00m, 50.00m);
        if (key.Contains("opus", StringComparison.Ordinal))
            return (5.00m, 25.00m);
        if (key.Contains("haiku", StringComparison.Ordinal))
            return (1.00m, 5.00m);
        if (key.Contains("sonnet", StringComparison.Ordinal))
            return (3.00m, 15.00m);
        if (key.Contains("gpt-5.6-luna", StringComparison.Ordinal) || key.Contains("gpt-5-nano", StringComparison.Ordinal))
            return (0.20m, 1.20m);
        if (key.Contains("gpt-5-mini", StringComparison.Ordinal))
            return (0.25m, 2.00m);
        if (key.Contains("gpt-4.1-mini", StringComparison.Ordinal))
            return (0.40m, 1.60m);
        if (key.Contains("gpt-4.1", StringComparison.Ordinal))
            return (2.00m, 8.00m);
        if (key.Contains("gpt-5.6-sol", StringComparison.Ordinal))
            return (4.00m, 20.00m);
        if (key.Contains("gpt-5", StringComparison.Ordinal))
            return (1.25m, 10.00m);
        if (key.Contains("grok-4.3", StringComparison.Ordinal) || key.Contains("grok-build", StringComparison.Ordinal))
            return (1.25m, 2.50m);
        if (key.Contains("grok", StringComparison.Ordinal))
            return (2.00m, 6.00m);
        return (3.00m, 15.00m);
    }
}
