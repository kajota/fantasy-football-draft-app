using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;
using FantasyDraftAssistant.Data.Database;
using Microsoft.Data.Sqlite;

namespace FantasyDraftAssistant.Data.Services;

public sealed class DraftCommandService(
    SqliteConnectionFactory factory,
    IDraftChangeNotifier notifier,
    IBackupService backups) : IDraftCommandService
{
    public Task<DraftCommitResult> StartDraftAsync(StartDraftCommand command, CancellationToken cancellationToken = default) =>
        CommitAsync(command.DraftId, null, state => DraftEngine.StartDraft(state, command), cancellationToken);

    public Task<DraftCommitResult> DraftPlayerAsync(DraftPlayerCommand command, CancellationToken cancellationToken = default) =>
        CommitAsync(command.DraftId, null, state => DraftEngine.DraftPlayer(state, command), cancellationToken);

    public async Task<DraftCommitResult> RollbackAsync(RollbackDraftCommand command, CancellationToken cancellationToken = default)
    {
        await backups.CreateBackupAsync("rollback", cancellationToken);
        return await CommitAsync(command.DraftId, null, state => DraftEngine.Rollback(state, command), cancellationToken);
    }

    public Task<DraftCommitResult> RedoAsync(RedoDraftCommand command, CancellationToken cancellationToken = default) =>
        CommitAsync(command.DraftId, null, state => DraftEngine.Redo(state, command), cancellationToken);

    public async Task<DraftCommitResult> CorrectPickAsync(CorrectPickCommand command, CancellationToken cancellationToken = default)
    {
        await backups.CreateBackupAsync("correction", cancellationToken);
        return await CommitAsync(command.DraftId, null, state => DraftEngine.CorrectPick(state, command), cancellationToken);
    }

    public async Task<DraftCommitResult> CreateBranchAsync(CreateDraftBranchCommand command, CancellationToken cancellationToken = default)
    {
        await backups.CreateBackupAsync("branch", cancellationToken);
        return await CommitAsync(command.DraftId, null, state => DraftEngine.CreateBranch(state, command), cancellationToken);
    }

    public Task<DraftCommitResult> SwitchBranchAsync(SwitchDraftBranchCommand command, CancellationToken cancellationToken = default) =>
        CommitAsync(command.DraftId, command.BranchId, state =>
        {
            var branch = state.ActiveBranch;
            return DraftEngine.SwitchBranch(state, branch);
        }, cancellationToken);

    public Task<DraftCommitResult> CompleteDraftAsync(CompleteDraftCommand command, CancellationToken cancellationToken = default) =>
        CommitAsync(command.DraftId, null, state => DraftEngine.Complete(state, command), cancellationToken);

    private Task<DraftCommitResult> CommitAsync(
        DraftId draftId,
        BranchId? branchId,
        Func<DraftWorkingState, DraftCommitResult> apply,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        DraftWorkingState state;
        try
        {
            state = DraftStateLoader.Load(db, tx, draftId, branchId);
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return Task.FromResult(DraftCommitResult.Fail(ex.Message));
        }

        var result = apply(state);
        if (!result.Succeeded)
        {
            tx.Rollback();
            return Task.FromResult(result);
        }

        Persist(db, tx, state);
        tx.Commit();
        notifier.Notify(state.Draft.DraftId, state.ActiveBranch.BranchId, state.Draft.CurrentStateVersion);
        return Task.FromResult(result);
    }

    internal static void Persist(SqliteConnection db, SqliteTransaction tx, DraftWorkingState state)
    {
        using (var cmd = db.Cmd("""
            UPDATE Drafts
            SET Status = $status,
                ActiveBranchId = $branch,
                CurrentStateVersion = $version,
                StartedAt = $started,
                CompletedAt = $completed
            WHERE DraftId = $id;
            """, tx)
                   .Bind("$status", state.Draft.Status.ToString())
                   .Bind("$branch", state.Draft.ActiveBranchId.ToString())
                   .Bind("$version", state.Draft.CurrentStateVersion)
                   .Bind("$started", state.Draft.StartedAt?.ToString("O"))
                   .Bind("$completed", state.Draft.CompletedAt?.ToString("O"))
                   .Bind("$id", state.Draft.DraftId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        if (state.CreatedBranch is { } created)
        {
            using var cmd = db.Cmd("""
                INSERT INTO DraftBranches(BranchId, DraftId, Name, ParentBranchId, BranchPointOverallPick, CreatedFromStateVersion, CurrentHeadEventId, CreatedAt)
                VALUES ($id, $draft, $name, $parent, $point, $from, $head, $created);
                """, tx)
                .Bind("$id", created.BranchId.ToString())
                .Bind("$draft", created.DraftId.ToString())
                .Bind("$name", created.Name)
                .Bind("$parent", created.ParentBranchId?.ToString())
                .Bind("$point", created.BranchPointOverallPick)
                .Bind("$from", created.CreatedFromStateVersion)
                .Bind("$head", created.CurrentHeadEventId?.ToString())
                .Bind("$created", created.CreatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }

        using (var cmd = db.Cmd("""
            UPDATE DraftBranches SET CurrentHeadEventId = $head WHERE BranchId = $id;
            """, tx)
                   .Bind("$head", state.ActiveBranch.CurrentHeadEventId?.ToString())
                   .Bind("$id", state.ActiveBranch.BranchId.ToString()))
        {
            cmd.ExecuteNonQuery();
        }

        foreach (var slot in state.Slots.Where(s => s.IsKeeperSlot))
        {
            using var cmd = db.Cmd("UPDATE DraftSlots SET IsKeeperSlot = 1 WHERE DraftSlotId = $id;", tx)
                .Bind("$id", slot.DraftSlotId.ToString());
            cmd.ExecuteNonQuery();
        }

        foreach (var evt in state.NewEvents)
        {
            using var cmd = db.Cmd("""
                INSERT INTO DraftEvents(EventId, DraftId, BranchId, EventType, StateVersion, SequenceNumber, CreatedAt, CreatedBy, CorrelationId, PayloadJson)
                VALUES ($id, $draft, $branch, $type, $version, $seq, $created, $by, $corr, $payload);
                """, tx)
                .Bind("$id", evt.EventId.ToString())
                .Bind("$draft", evt.DraftId.ToString())
                .Bind("$branch", evt.BranchId.ToString())
                .Bind("$type", evt.EventType.ToString())
                .Bind("$version", evt.StateVersion)
                .Bind("$seq", evt.SequenceNumber)
                .Bind("$created", evt.CreatedAt.ToString("O"))
                .Bind("$by", evt.CreatedBy)
                .Bind("$corr", evt.CorrelationId)
                .Bind("$payload", evt.PayloadJson);
            cmd.ExecuteNonQuery();
        }

        WriteProjections(db, tx, state);
    }

    private static void WriteProjections(SqliteConnection db, SqliteTransaction tx, DraftWorkingState state)
    {
        var draft = state.Draft.DraftId.ToString();
        var branch = state.ActiveBranch.BranchId.ToString();

        using (var cmd = db.Cmd("DELETE FROM ActiveDraftSelections WHERE DraftId = $d AND BranchId = $b;", tx)
                   .Bind("$d", draft).Bind("$b", branch))
            cmd.ExecuteNonQuery();
        using (var cmd = db.Cmd("DELETE FROM TeamRosterProjection WHERE DraftId = $d AND BranchId = $b;", tx)
                   .Bind("$d", draft).Bind("$b", branch))
            cmd.ExecuteNonQuery();
        using (var cmd = db.Cmd("DELETE FROM PlayerAvailabilityProjection WHERE DraftId = $d AND BranchId = $b;", tx)
                   .Bind("$d", draft).Bind("$b", branch))
            cmd.ExecuteNonQuery();

        foreach (var selection in state.ActiveSelections.Values)
        {
            using (var cmd = db.Cmd("""
                INSERT INTO ActiveDraftSelections(DraftId, BranchId, OverallPick, EventId, DraftSlotId, Round, RoundPick, TeamId, PlayerId, Source, ExternalSourceId, ObservedAt)
                VALUES ($d, $b, $pick, $event, $slot, $round, $rp, $team, $player, $source, $ext, $obs);
                """, tx)
                       .Bind("$d", draft)
                       .Bind("$b", branch)
                       .Bind("$pick", selection.OverallPick)
                       .Bind("$event", selection.EventId.ToString())
                       .Bind("$slot", selection.DraftSlotId.ToString())
                       .Bind("$round", selection.Round)
                       .Bind("$rp", selection.RoundPick)
                       .Bind("$team", selection.TeamId.ToString())
                       .Bind("$player", selection.PlayerId.ToString())
                       .Bind("$source", selection.Source.ToString())
                       .Bind("$ext", selection.ExternalSourceId)
                       .Bind("$obs", selection.ObservedAt.ToString("O")))
            {
                cmd.ExecuteNonQuery();
            }

            using (var cmd = db.Cmd("""
                INSERT INTO TeamRosterProjection(DraftId, BranchId, TeamId, PlayerId, OverallPick)
                VALUES ($d, $b, $team, $player, $pick);
                """, tx)
                       .Bind("$d", draft)
                       .Bind("$b", branch)
                       .Bind("$team", selection.TeamId.ToString())
                       .Bind("$player", selection.PlayerId.ToString())
                       .Bind("$pick", selection.OverallPick))
            {
                cmd.ExecuteNonQuery();
            }
        }

        using var playerCmd = db.Cmd("SELECT PlayerId FROM Players;", tx);
        using var reader = playerCmd.ExecuteReader();
        var playerIds = new List<string>();
        while (reader.Read())
            playerIds.Add(reader.GetString(0));
        reader.Close();

        foreach (var playerId in playerIds)
        {
            var available = !state.UnavailablePlayers.Contains(PlayerId.Parse(playerId));
            using var cmd = db.Cmd("""
                INSERT INTO PlayerAvailabilityProjection(DraftId, BranchId, PlayerId, IsAvailable)
                VALUES ($d, $b, $p, $a);
                """, tx)
                .Bind("$d", draft)
                .Bind("$b", branch)
                .Bind("$p", playerId)
                .Bind("$a", available ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        using (var cmd = db.Cmd("DELETE FROM DraftQueueItems WHERE DraftId = $d AND BranchId = $b;", tx)
                   .Bind("$d", draft).Bind("$b", branch))
            cmd.ExecuteNonQuery();
        foreach (var item in state.Queue)
        {
            using var cmd = db.Cmd("""
                INSERT INTO DraftQueueItems(QueueItemId, DraftId, BranchId, PlayerId, SortOrder, CreatedAt)
                VALUES ($id, $d, $b, $p, $s, $c);
                """, tx)
                .Bind("$id", item.QueueItemId.ToString())
                .Bind("$d", draft)
                .Bind("$b", branch)
                .Bind("$p", item.PlayerId.ToString())
                .Bind("$s", item.SortOrder)
                .Bind("$c", item.CreatedAt.ToString("O"));
            cmd.ExecuteNonQuery();
        }
    }
}
