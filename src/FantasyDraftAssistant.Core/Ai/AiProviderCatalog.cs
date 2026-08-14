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
            "API key from platform.openai.com. A ChatGPT Plus or Pro subscription does not include API access."),
        new(
            Anthropic,
            "Claude",
            "Anthropic",
            "claude-sonnet-4-6",
            "API key from console.anthropic.com. A Claude Pro subscription does not include API access."),
        new(
            Xai,
            "Grok",
            "xAI",
            "grok-4.6",
            "API key from console.x.ai. SuperGrok does not include API access.")
    ];

    public static AiProviderDescriptor? Find(string providerKey) =>
        All.FirstOrDefault(p => p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));
}

public sealed record AiProviderDescriptor(
    string ProviderKey,
    string ProductName,
    string CompanyName,
    string DefaultModel,
    string CredentialHelp);
