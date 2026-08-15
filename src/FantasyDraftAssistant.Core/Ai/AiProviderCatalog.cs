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
            "gpt-4.1",
            "API key from platform.openai.com. ChatGPT Plus does not pay for the API — add a payment method under platform.openai.com billing.",
            ["gpt-4.1", "gpt-5", "gpt-5-mini", "gpt-4.1-mini"]),
        new(
            Anthropic,
            "Claude",
            "Anthropic",
            "claude-sonnet-4-6",
            "API key from console.anthropic.com. A Claude Pro subscription does not include API access.",
            [
                "claude-sonnet-4-6",
                "claude-sonnet-5",
                "claude-opus-5",
                "claude-opus-4-8",
                "claude-haiku-4-5"
            ]),
        new(
            Xai,
            "Grok",
            "xAI",
            "grok-4.6",
            "API key from console.x.ai. SuperGrok does not include API access.",
            ["grok-4.6", "grok-4", "grok-3"])
    ];

    public static AiProviderDescriptor? Find(string providerKey) =>
        All.FirstOrDefault(p => p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));
}

public sealed record AiProviderDescriptor(
    string ProviderKey,
    string ProductName,
    string CompanyName,
    string DefaultModel,
    string CredentialHelp,
    IReadOnlyList<string> SuggestedModels);
