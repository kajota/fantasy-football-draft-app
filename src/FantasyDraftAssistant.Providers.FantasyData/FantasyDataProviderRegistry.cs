using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.FantasyData;

public sealed class FantasyDataProviderRegistry(IEnumerable<IFantasyDataProvider> providers) : IFantasyDataProviderRegistry
{
    private readonly IReadOnlyList<IFantasyDataProvider> _providers = providers.ToList();

    public IReadOnlyList<IFantasyDataProvider> All => _providers;

    public IFantasyDataProvider? Get(string providerKey) =>
        _providers.FirstOrDefault(p => p.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase));

    public async Task<FantasyDataRefreshResult> RefreshPreferredAsync(
        FantasyDataRefreshRequest request,
        CancellationToken cancellationToken)
    {
        var sleeper = Get(SleeperFantasyDataProvider.Key);
        string? sleeperError = null;
        if (sleeper is not null)
        {
            var live = await sleeper.RefreshAsync(request, cancellationToken);
            if (live.Succeeded)
                return live;
            sleeperError = live.Error;
        }

        var seed = Get(SeedFantasyDataProvider.Key);
        if (seed is null)
        {
            return FantasyDataRefreshResult.Fail(sleeperError
                ?? "No fantasy-data provider is registered.");
        }

        var fallback = await seed.RefreshAsync(request, cancellationToken);
        if (!fallback.Succeeded)
            return fallback;

        return new FantasyDataRefreshResult
        {
            Succeeded = true,
            Error = sleeperError is null
                ? null
                : $"Sleeper was unavailable ({sleeperError}). Loaded the offline seed instead.",
            PlayersWritten = fallback.PlayersWritten,
            RankingsWritten = fallback.RankingsWritten,
            AdpWritten = fallback.AdpWritten,
            ProjectionsWritten = fallback.ProjectionsWritten,
            RefreshedAt = fallback.RefreshedAt
        };
    }
}
