using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Data.Services;

public sealed class DraftChangeNotifier : IDraftChangeNotifier
{
    public event EventHandler<DraftChangedEventArgs>? DraftChanged;

    public void Notify(DraftId draftId, BranchId branchId, int stateVersion)
    {
        DraftChanged?.Invoke(this, new DraftChangedEventArgs
        {
            DraftId = draftId,
            BranchId = branchId,
            StateVersion = stateVersion
        });
    }
}
