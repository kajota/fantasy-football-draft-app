using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Providers.AI;

public sealed class AiProviderRegistry(IEnumerable<IAiProviderAdapter> adapters) : IAiProviderRegistry
{
    private readonly IReadOnlyList<IAiProviderAdapter> _adapters = adapters.ToList();

    public IReadOnlyList<IAiProviderAdapter> All => _adapters;

    public IAiProviderAdapter? Get(string providerKey) =>
        _adapters.FirstOrDefault(a => a.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));
}
