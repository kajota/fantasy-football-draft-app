using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class DraftStateService(SqliteConnectionFactory factory) : IDraftStateService
{
    public Task<DraftWorkingState?> GetWorkingStateAsync(DraftId draftId, BranchId? branchId = null, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        var draft = LeagueService.LoadDraft(db, null, draftId);
        if (draft is null)
            return Task.FromResult<DraftWorkingState?>(null);
        return Task.FromResult<DraftWorkingState?>(DraftStateLoader.Load(db, null, draftId, branchId));
    }

    public Task<IReadOnlyList<Player>> GetPlayersAsync(CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<Player>>(LeagueService.LoadPlayers(db));
    }

    public Task<IReadOnlyList<DraftQueueItem>> GetQueueAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        return Task.FromResult<IReadOnlyList<DraftQueueItem>>(LeagueService.LoadQueue(db, null, draftId, branchId));
    }

    public Task AddToQueueAsync(QueueAddCommand command, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        var draft = LeagueService.LoadDraft(db, null, command.DraftId)
                    ?? throw new InvalidOperationException("Draft not found.");
        using var tx = db.BeginTransaction();
        using (var exists = db.Cmd("""
            SELECT 1 FROM DraftQueueItems WHERE DraftId = $d AND BranchId = $b AND PlayerId = $p;
            """, tx)
                   .Bind("$d", command.DraftId.ToString())
                   .Bind("$b", draft.ActiveBranchId.ToString())
                   .Bind("$p", command.PlayerId.ToString()))
        {
            if (exists.ExecuteScalar() is not null)
            {
                tx.Commit();
                return Task.CompletedTask;
            }
        }

        int next;
        using (var cmd = db.Cmd("SELECT COALESCE(MAX(SortOrder), 0) + 1 FROM DraftQueueItems WHERE DraftId = $d AND BranchId = $b;", tx)
                   .Bind("$d", command.DraftId.ToString())
                   .Bind("$b", draft.ActiveBranchId.ToString()))
        {
            next = Convert.ToInt32(cmd.ExecuteScalar());
        }

        using (var cmd = db.Cmd("""
            INSERT INTO DraftQueueItems(QueueItemId, DraftId, BranchId, PlayerId, SortOrder, CreatedAt)
            VALUES ($id, $d, $b, $p, $s, $c);
            """, tx)
                   .Bind("$id", QueueItemId.New().ToString())
                   .Bind("$d", command.DraftId.ToString())
                   .Bind("$b", draft.ActiveBranchId.ToString())
                   .Bind("$p", command.PlayerId.ToString())
                   .Bind("$s", next)
                   .Bind("$c", DateTimeOffset.UtcNow.ToString("O")))
        {
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task RemoveFromQueueAsync(QueueRemoveCommand command, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var cmd = db.Cmd("DELETE FROM DraftQueueItems WHERE QueueItemId = $id;")
            .Bind("$id", command.QueueItemId.ToString());
        cmd.ExecuteNonQuery();
        return Task.CompletedTask;
    }

    public Task ReorderQueueAsync(QueueReorderCommand command, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        var order = 1;
        foreach (var id in command.OrderedIds)
        {
            using var cmd = db.Cmd("UPDATE DraftQueueItems SET SortOrder = $s WHERE QueueItemId = $id;", tx)
                .Bind("$s", order++)
                .Bind("$id", id.ToString());
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
        return Task.CompletedTask;
    }

    public Task<IntegrityReport> ValidateAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        using var db = factory.Open();
        var state = DraftStateLoader.Load(db, null, draftId);
        return Task.FromResult(IntegrityChecker.Validate(state));
    }
}
