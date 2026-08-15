using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.FantasyData;

public sealed class SeedFantasyDataProvider(IFantasyDataWriter writer) : IFantasyDataProvider
{
    public const string Key = "seed";
    public string ProviderKey => Key;
    public string DisplayName => "Built-in seed";
    public string Description => "Offline sample players for testing when the network is down.";

    public Task<FantasyDataRefreshResult> RefreshAsync(FantasyDataRefreshRequest request, CancellationToken cancellationToken)
    {
        var data = SeedCatalog.Materialize(Key);
        return writer.WriteAsync(Key, data.Players, data.Ids, data.Rankings, data.Adp, data.Projections, cancellationToken);
    }
}
