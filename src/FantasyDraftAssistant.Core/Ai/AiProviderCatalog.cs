namespace FantasyDraftAssistant.Core.Ai;

public static class AiProviderCatalog
{
    public const string OpenAi = "openai";
    public const string Anthropic = "anthropic";
    public const string Xai = "xai";

    public static IReadOnlyList<string> Roles { get; } =
    [
        "Fast Advisor",
        "Deep Advisor",
        "Draft Watcher",
        "Secondary Opinion"
    ];

    public static IReadOnlyList<AiProviderDescriptor> All { get; } =
    [
        new(
            OpenAi,
            "ChatGPT",
            "OpenAI",
            "gpt-5.6-luna",
            "API key from platform.openai.com. ChatGPT Plus does not pay for the API — add a payment method under platform.openai.com billing. Luna is the cheap GPT-5.6 pick for draft advice; Sol is the expensive flagship.",
            [
                Model(
                    "gpt-5.6-luna",
                    "best value",
                    0.20m,
                    1.20m,
                    "Cheapest current GPT-5.6. Plenty for draft asks. Uses hidden reasoning, so Fast/Deep still matter."),
                Model(
                    "gpt-5-mini",
                    "cheap GPT-5",
                    0.25m,
                    2.00m,
                    "Older cheap GPT-5. Fine if Luna is unavailable."),
                Model(
                    "gpt-4.1-mini",
                    "cheap, no reasoning tax",
                    0.40m,
                    1.60m,
                    "No hidden reasoning tokens. Predictable cost and faster replies than GPT-5.x."),
                Model(
                    "gpt-5.6-terra",
                    "balanced",
                    2.00m,
                    12.00m,
                    "Mid GPT-5.6. Use if Luna feels thin on a Deep ask."),
                Model(
                    "gpt-4.1",
                    "previous default",
                    2.00m,
                    8.00m,
                    "Solid older model with no hidden reasoning. More expensive than Luna or 4.1 Mini."),
                Model(
                    "gpt-5.6-sol",
                    "flagship, expensive",
                    4.00m,
                    20.00m,
                    "OpenAI's top model. Overkill for pick advice. Skip unless you want to spend for it.")
            ]),
        new(
            Anthropic,
            "Claude",
            "Anthropic",
            "claude-sonnet-5",
            "API key from console.anthropic.com. A Claude Pro subscription does not include API access. Sonnet 5 is the sweet spot; Haiku is cheaper; Opus and Fable are overkill for draft advice.",
            [
                Model(
                    "claude-haiku-4-5",
                    "cheapest",
                    1.00m,
                    5.00m,
                    "Fastest Claude. Near-frontier quality at the lowest Claude price. Great Fast Advisor."),
                Model(
                    "claude-sonnet-5",
                    "recommended",
                    2.00m,
                    10.00m,
                    "Best Claude for this app: strong advice without Opus prices."),
                Model(
                    "claude-sonnet-4-6",
                    "previous default",
                    3.00m,
                    15.00m,
                    "Older Sonnet. Same job as Sonnet 5, costs more."),
                Model(
                    "claude-opus-5",
                    "expensive",
                    5.00m,
                    25.00m,
                    "Frontier Claude. Rarely worth it for a pick recommendation."),
                Model(
                    "claude-fable-5",
                    "overkill",
                    10.00m,
                    50.00m,
                    "Anthropic's most expensive model. Do not use for routine draft asks.")
            ]),
        new(
            Xai,
            "Grok",
            "xAI",
            "grok-4.3",
            "API key from console.x.ai. SuperGrok does not include API access. Grok 4.3 is the value pick; 4.6 is the current flagship at a higher price.",
            [
                Model(
                    "grok-4.3",
                    "best value",
                    1.25m,
                    2.50m,
                    "Strong Grok at about half the output price of 4.6. Best default for draft advice."),
                Model(
                    "grok-build-0.1",
                    "cheapest",
                    1.00m,
                    2.00m,
                    "Lowest Grok text price. Tuned for code, so draft prose may be thinner."),
                Model(
                    "grok-4.5",
                    "previous flagship",
                    2.00m,
                    6.00m,
                    "Same price as 4.6, slightly older. Prefer 4.3 unless you already like 4.5."),
                Model(
                    "grok-4.6",
                    "flagship",
                    2.00m,
                    6.00m,
                    "Current xAI flagship. Use for Deep Advisor if 4.3 feels light.")
            ])
    ];

    public static AiProviderDescriptor? Find(string providerKey) =>
        All.FirstOrDefault(p => p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));

    public static AiModelOption? FindModel(string? model)
    {
        var key = (model ?? "").Trim();
        if (key.Length == 0)
            return null;

        foreach (var provider in All)
        {
            var exact = provider.SuggestedModels.FirstOrDefault(m =>
                m.Id.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
                return exact;
        }

        foreach (var provider in All)
        {
            var dated = provider.SuggestedModels.FirstOrDefault(m =>
                key.StartsWith(m.Id + "-", StringComparison.OrdinalIgnoreCase));
            if (dated is not null)
                return dated;
        }

        return null;
    }

    public static AiModelOption Model(
        string id,
        string tag,
        decimal inputPerMillion,
        decimal outputPerMillion,
        string summary) =>
        new(
            id,
            $"{id}  —  {tag}  ·  ${FormatUsd(inputPerMillion)} / ${FormatUsd(outputPerMillion)}",
            summary,
            inputPerMillion,
            outputPerMillion);

    private static string FormatUsd(decimal value) =>
        value == decimal.Truncate(value) ? value.ToString("0") : value.ToString("0.00");
}

public sealed record AiProviderDescriptor(
    string ProviderKey,
    string ProductName,
    string CompanyName,
    string DefaultModel,
    string CredentialHelp,
    IReadOnlyList<AiModelOption> SuggestedModels)
{
    public IReadOnlyList<string> SuggestedModelIds =>
        SuggestedModels.Select(m => m.Id).ToList();
}

public sealed record AiModelOption(
    string Id,
    string DisplayName,
    string Summary,
    decimal InputPerMillion,
    decimal OutputPerMillion)
{
    public override string ToString() => Id;
}
