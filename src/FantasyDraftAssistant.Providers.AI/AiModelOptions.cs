using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Providers.AI;

public sealed class AiModelOptions(
    IAiProviderRegistry providers,
    IAiConfigStore configs,
    ICredentialStore credentials) : IAiModelOptions
{
    public async Task<IReadOnlyList<AiModelChoice>> ListEnabledAsync(CancellationToken cancellationToken = default)
    {
        var saved = await configs.ListAsync(cancellationToken);
        var choices = new List<AiModelChoice>();

        foreach (var config in saved)
        {
            if (!config.Enabled)
                continue;
            if (providers.Get(config.ProviderKey) is null)
                continue;

            var key = await credentials.GetSecretAsync("ai", config.ProviderKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            choices.Add(new AiModelChoice
            {
                ProviderKey = config.ProviderKey,
                Model = config.Model,
                DisplayName = AiProviderCatalog.Find(config.ProviderKey)?.ProductName ?? config.ProviderKey,
                Role = config.Role
            });
        }

        return choices
            .OrderByDescending(choice =>
                string.Equals(choice.Role, AiAnalysisMode.FastAdvisor, StringComparison.OrdinalIgnoreCase))
            .ThenBy(choice => choice.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
