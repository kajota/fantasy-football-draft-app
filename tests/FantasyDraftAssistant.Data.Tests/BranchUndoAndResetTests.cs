using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Providers.FantasyData;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class BranchUndoAndResetTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public BranchUndoAndResetTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        collection.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    public void Dispose()
    {
        _services.Dispose();
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_root, true); } catch (IOException) { }
        GC.SuppressFinalize(this);
    }

    private IDraftCommandService Commands => _services.GetRequiredService<IDraftCommandService>();
    private IDraftStateService States => _services.GetRequiredService<IDraftStateService>();

    private async Task<DraftId> StartedDraftAsync()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);

        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Undo League",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Undo Draft"
        });
        Assert.True((await Commands.StartDraftAsync(new StartDraftCommand(draft.DraftId))).Succeeded);
        return draft.DraftId;
    }

    private async Task DraftAsync(DraftId draftId, string name, string position)
    {
        var result = await Commands.DraftPlayerAsync(
            new DraftPlayerCommand(draftId, PlayerId.FromName(name, position)));
        Assert.True(result.Succeeded, result.Error);
    }

    private async Task<int> PickCountAsync(DraftId draftId)
    {
        var state = await States.GetWorkingStateAsync(draftId);
        return state!.ActiveSelections.Count;
    }

    /// <summary>
    /// The bug this covers: a practice branch inherited the parent's picks whenever it had no
    /// rows of its own, so undoing the last inherited pick appeared to succeed and then the
    /// pick came straight back on the next load.
    /// </summary>
    [Fact]
    public async Task Undo_on_a_practice_branch_does_not_resurrect_the_inherited_pick()
    {
        var draftId = await StartedDraftAsync();
        await DraftAsync(draftId, "Bijan Robinson", "RB");
        Assert.Equal(1, await PickCountAsync(draftId));

        // Practice from the current pick, which inherits the one pick already made.
        var branch = await Commands.CreateBranchAsync(new CreateDraftBranchCommand(draftId, "Practice", 2));
        Assert.True(branch.Succeeded, branch.Error);
        Assert.Equal(1, await PickCountAsync(draftId));

        var undo = await Commands.RollbackAsync(new RollbackDraftCommand(draftId, 0));
        Assert.True(undo.Succeeded, undo.Error);

        // Before the fix this read 1 again, because an empty branch fell back to the parent.
        Assert.Equal(0, await PickCountAsync(draftId));
    }

    [Fact]
    public async Task A_fresh_practice_branch_still_inherits_picks_before_its_branch_point()
    {
        var draftId = await StartedDraftAsync();
        await DraftAsync(draftId, "Bijan Robinson", "RB");
        await DraftAsync(draftId, "Ja'Marr Chase", "WR");

        var branch = await Commands.CreateBranchAsync(new CreateDraftBranchCommand(draftId, "Practice", 3));
        Assert.True(branch.Succeeded, branch.Error);

        // Inheritance itself must keep working; only the empty-means-inherit guess is gone.
        Assert.Equal(2, await PickCountAsync(draftId));
    }

    [Fact]
    public async Task Reset_clears_every_pick_in_one_step()
    {
        var draftId = await StartedDraftAsync();
        await DraftAsync(draftId, "Bijan Robinson", "RB");
        await DraftAsync(draftId, "Ja'Marr Chase", "WR");
        await DraftAsync(draftId, "Justin Jefferson", "WR");
        Assert.Equal(3, await PickCountAsync(draftId));

        var reset = await Commands.ResetAsync(new ResetDraftCommand(draftId));
        Assert.True(reset.Succeeded, reset.Error);
        Assert.Equal(0, await PickCountAsync(draftId));
    }

    [Fact]
    public async Task Reset_is_one_undo_away()
    {
        var draftId = await StartedDraftAsync();
        await DraftAsync(draftId, "Bijan Robinson", "RB");
        await DraftAsync(draftId, "Ja'Marr Chase", "WR");

        Assert.True((await Commands.ResetAsync(new ResetDraftCommand(draftId))).Succeeded);
        Assert.Equal(0, await PickCountAsync(draftId));

        var redo = await Commands.RedoAsync(new RedoDraftCommand(draftId));
        Assert.True(redo.Succeeded, redo.Error);
        Assert.Equal(2, await PickCountAsync(draftId));
    }

    [Fact]
    public async Task Reset_on_an_empty_board_reports_why_instead_of_doing_nothing()
    {
        var draftId = await StartedDraftAsync();

        var reset = await Commands.ResetAsync(new ResetDraftCommand(draftId));
        Assert.False(reset.Succeeded);
        Assert.Equal("This board has no picks to clear.", reset.Error);
    }

    [Fact]
    public async Task Reset_leaves_the_draft_in_progress_so_drafting_can_continue()
    {
        var draftId = await StartedDraftAsync();
        await DraftAsync(draftId, "Bijan Robinson", "RB");
        Assert.True((await Commands.ResetAsync(new ResetDraftCommand(draftId))).Succeeded);

        var state = await States.GetWorkingStateAsync(draftId);
        Assert.Equal(DraftStatus.InProgress, state!.Draft.Status);
        Assert.Equal(1, state.CurrentSlot!.OverallPick);

        // The player freed by the reset can be taken again.
        await DraftAsync(draftId, "Bijan Robinson", "RB");
        Assert.Equal(1, await PickCountAsync(draftId));
    }
}
