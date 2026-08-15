using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Data.Services;

public sealed class AnalyticsService(IDraftStateService drafts, IFantasyDataWriter fantasyData) : IAnalyticsService
{
    public async Task<AnalyticsSnapshot> GetSnapshotAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default)
    {
        var state = await drafts.GetWorkingStateAsync(draftId, branchId, cancellationToken)
                    ?? throw new InvalidOperationException("Draft not found.");
        var players = await drafts.GetPlayersAsync(cancellationToken);
        var format = FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots);
        var sourceKey = FantasyDataSourcePicker.Pick(await fantasyData.GetSourceKeysAsync(cancellationToken), format);
        var rankings = await fantasyData.GetRankingsAsync(sourceKey, cancellationToken);
        var adp = await fantasyData.GetAdpAsync(sourceKey, cancellationToken);
        if (rankings.Count == 0)
            rankings = await fantasyData.GetRankingsAsync(cancellationToken: cancellationToken);
        if (adp.Count == 0)
            adp = await fantasyData.GetAdpAsync(cancellationToken: cancellationToken);
        return AnalyticsEngine.Compute(state, players, rankings, adp);
    }
}
