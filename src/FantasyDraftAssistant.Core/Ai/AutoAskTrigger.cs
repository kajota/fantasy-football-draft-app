using FantasyDraftAssistant.Core.Ids;

namespace FantasyDraftAssistant.Core.Ai;

public static class AutoAskTrigger
{
    public static bool ShouldAsk(
        bool enabled,
        bool onClock,
        BranchId? branchId,
        int overallPick,
        BranchId? lastBranchId,
        int lastOverallPick) =>
        enabled
        && onClock
        && overallPick > 0
        && (lastBranchId is null
            || lastOverallPick < 0
            || !lastBranchId.Equals(branchId)
            || lastOverallPick != overallPick);
}
