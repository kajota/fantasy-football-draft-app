using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;

namespace FantasyDraftAssistant.Data.Services;

public sealed class NoOpBoardPublisher : IBoardPublisher
{
    public string LastStatus => "Not published yet.";
    public event EventHandler? StatusChanged
    {
        add { }
        remove { }
    }

    public void Schedule(DraftId draftId, BranchId branchId)
    {
    }

    public Task PublishNowAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}
