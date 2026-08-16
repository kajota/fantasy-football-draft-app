using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Data;
using FantasyDraftAssistant.Data.Database;
using FantasyDraftAssistant.Providers.FantasyData;
using Microsoft.Extensions.DependencyInjection;

namespace FantasyDraftAssistant.Data.Tests;

public class PersistenceTests : IDisposable
{
    private readonly string _root;
    private readonly ServiceProvider _services;

    public PersistenceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fda-tests", Guid.NewGuid().ToString("N"));
        var collection = new ServiceCollection();
        collection.AddFantasyDraftData(_root);
        collection.AddSingleton<IFantasyDataProvider, SeedFantasyDataProvider>();
        _services = collection.BuildServiceProvider();
        _services.GetRequiredService<MigrationRunner>().Apply();
    }

    [Fact]
    public async Task Failed_command_does_not_advance_draft_state()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        var failed = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, PlayerId.New()));
        Assert.False(failed.Succeeded);

        var second = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, PlayerId.New()));
        Assert.False(second.Succeeded);

        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        Assert.Empty(state.ActiveSelections);
        Assert.Equal(1, state.Draft.CurrentStateVersion);
        Assert.Equal(1, state.CurrentOverallPick);
    }

    [Fact]
    public async Task Pick_persists_and_survives_reload()
    {
        var (draftId, player) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        var result = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, player));
        Assert.True(result.Succeeded, result.Error);

        var reloaded = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(reloaded);
        Assert.Single(reloaded.ActiveSelections);
        Assert.Equal(player, reloaded.ActiveSelections[1].PlayerId);
        Assert.Equal(2, reloaded.CurrentOverallPick);
        Assert.True(reloaded.UnavailablePlayers.Contains(player));
    }

    [Fact]
    public async Task Rollback_and_rebuild_from_events_restore_availability()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var second = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, second))).Succeeded);
        var rollback = await commands.RollbackAsync(new RollbackDraftCommand(draftId, 1));
        Assert.True(rollback.Succeeded, rollback.Error);

        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        Assert.Single(state.ActiveSelections);
        Assert.False(state.UnavailablePlayers.Contains(second));
    }

    [Fact]
    public async Task Archive_hides_league_and_restore_returns_it()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Keep Me",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4
        });

        await leagues.ArchiveLeagueAsync(league.LeagueId);

        Assert.Empty(await leagues.ListLeaguesAsync());
        var archived = Assert.Single(await leagues.ListArchivedLeaguesAsync());
        Assert.Equal(league.LeagueId, archived.LeagueId);
        Assert.True(archived.IsArchived);

        var loaded = await leagues.GetLeagueAsync(league.LeagueId);
        Assert.NotNull(loaded);
        Assert.NotNull(loaded.ArchivedAt);

        await leagues.RestoreLeagueAsync(league.LeagueId);

        var active = Assert.Single(await leagues.ListLeaguesAsync());
        Assert.Equal(league.LeagueId, active.LeagueId);
        Assert.False(active.IsArchived);
        Assert.Empty(await leagues.ListArchivedLeaguesAsync());
    }

    [Fact]
    public async Task Permanent_delete_removes_league_and_draft_history()
    {
        var (draftId, player) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();
        var leagues = _services.GetRequiredService<ILeagueService>();
        var backups = _services.GetRequiredService<IBackupService>();

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, player))).Succeeded);
        var working = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(working);
        var leagueId = working.Draft.LeagueId;

        await leagues.DeleteLeaguePermanentlyAsync(leagueId);

        Assert.Empty(await leagues.ListLeaguesAsync());
        Assert.Empty(await leagues.ListArchivedLeaguesAsync());
        Assert.Null(await leagues.GetLeagueAsync(leagueId));
        Assert.Null(await states.GetWorkingStateAsync(draftId));
        Assert.NotEmpty(await backups.ListBackupsAsync());
    }

    [Fact]
    public async Task Archived_league_cannot_start_a_new_draft()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Parked",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4
        });
        await leagues.ArchiveLeagueAsync(league.LeagueId);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            leagues.CreateDraftAsync(new CreateDraftRequest
            {
                LeagueId = league.LeagueId,
                Name = "Should Fail"
            }));
        Assert.Contains("Restore", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Keepers_save_consume_slot_and_lock_after_start()
    {
        var (draftId, player, teamId) = await CreateUnstartedDraftAsync();
        var leagues = _services.GetRequiredService<ILeagueService>();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draftId,
            Keepers =
            [
                new KeeperSpec { TeamId = teamId, PlayerId = player, RoundCost = 2 }
            ]
        });

        var beforeStart = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(beforeStart);
        var marked = Assert.Single(beforeStart.Slots, slot => slot.IsKeeperSlot);
        Assert.Equal(2, marked.Round);
        Assert.Equal(teamId, marked.TeamId);

        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draftId,
            Keepers =
            [
                new KeeperSpec { TeamId = teamId, PlayerId = player, RoundCost = 3 }
            ]
        });

        var moved = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(moved);
        Assert.DoesNotContain(moved.Slots, slot => slot.IsKeeperSlot && slot.Round == 2);
        Assert.Contains(moved.Slots, slot => slot.IsKeeperSlot && slot.Round == 3 && slot.TeamId.Equals(teamId));

        var started = await commands.StartDraftAsync(new StartDraftCommand(draftId));
        Assert.True(started.Succeeded, started.Error);

        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        Assert.True(state.UnavailablePlayers.Contains(player));
        var selection = Assert.Single(state.ActiveSelections.Values, s => s.Source == PickSource.Keeper);
        Assert.Equal(3, selection.Round);
        Assert.Equal(player, selection.PlayerId);

        var other = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draftId,
            Keepers =
            [
                new KeeperSpec { TeamId = teamId, PlayerId = other, RoundCost = 2 }
            ]
        });

        var replaced = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(replaced);
        Assert.False(replaced.UnavailablePlayers.Contains(player));
        Assert.True(replaced.UnavailablePlayers.Contains(other));
        var replacedSelection = Assert.Single(replaced.ActiveSelections.Values);
        Assert.Equal(PickSource.Keeper, replacedSelection.Source);
        Assert.Equal(2, replacedSelection.Round);
        Assert.Equal(other, replacedSelection.PlayerId);
        Assert.Null(replaced.Redo);

        var drafted = await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, player));
        Assert.True(drafted.Succeeded, drafted.Error);

        var locked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            leagues.SaveKeepersAsync(new SaveKeepersRequest
            {
                DraftId = draftId,
                Keepers = []
            }));
        Assert.Contains("regular picks", locked.Message, StringComparison.OrdinalIgnoreCase);

        var lastHuman = (await states.GetWorkingStateAsync(draftId))!.ActiveSelections.Values
            .Where(s => s.Source != PickSource.Keeper)
            .Select(s => s.OverallPick)
            .Max();
        var undone = await commands.RollbackAsync(new RollbackDraftCommand(draftId, lastHuman - 1));
        Assert.True(undone.Succeeded, undone.Error);

        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draftId,
            Keepers =
            [
                new KeeperSpec { TeamId = teamId, PlayerId = player, RoundCost = 4 }
            ]
        });

        var afterUndo = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(afterUndo);
        var restored = Assert.Single(afterUndo.ActiveSelections.Values);
        Assert.Equal(PickSource.Keeper, restored.Source);
        Assert.Equal(4, restored.Round);
        Assert.Equal(player, restored.PlayerId);
        Assert.Null(afterUndo.Redo);
    }

    [Fact]
    public async Task Keepers_reject_two_for_the_same_team()
    {
        var (draftId, player, teamId) = await CreateUnstartedDraftAsync();
        var other = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        var leagues = _services.GetRequiredService<ILeagueService>();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            leagues.SaveKeepersAsync(new SaveKeepersRequest
            {
                DraftId = draftId,
                Keepers =
                [
                    new KeeperSpec { TeamId = teamId, PlayerId = player, RoundCost = 1 },
                    new KeeperSpec { TeamId = teamId, PlayerId = other, RoundCost = 2 }
                ]
            }));
        Assert.Contains("limit is 1", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Practice_forks_keepers_and_cpu_picks_until_user()
    {
        var (draftId, player, teamId) = await CreateUnstartedDraftAsync();
        var leagues = _services.GetRequiredService<ILeagueService>();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();
        var mock = _services.GetRequiredService<IMockDraftService>();
        var other = PlayerId.FromName("Saquon Barkley", "PHI", "RB");

        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draftId,
            Keepers = [new KeeperSpec { TeamId = teamId, PlayerId = other, RoundCost = 2 }]
        });

        var practice = await mock.StartPracticeAsync(draftId);
        Assert.True(practice.Succeeded, practice.Error);
        Assert.NotNull(practice.BranchId);

        var policies = await mock.GetPoliciesAsync(draftId, practice.BranchId.Value);
        Assert.Equal(4, policies.Count);
        Assert.Contains(policies, policy => !policy.IsCpu && policy.TeamId.Equals(teamId));
        Assert.Equal(3, policies.Count(policy => policy.IsCpu));

        var forked = await states.GetWorkingStateAsync(draftId, practice.BranchId);
        Assert.NotNull(forked);
        var keeper = Assert.Single(forked.ActiveSelections.Values, selection => selection.Source == PickSource.Keeper);
        Assert.Equal(other, keeper.PlayerId);

        var onClock = await mock.SimulateNextAsync(draftId, practice.BranchId);
        Assert.True(onClock.IsUserPick);

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, player))).Succeeded);

        var cpu = await mock.SimulateNextAsync(draftId, practice.BranchId);
        Assert.True(cpu.Succeeded, cpu.Error);
        Assert.False(cpu.IsUserPick);
        Assert.False(string.IsNullOrWhiteSpace(cpu.PlayerName));

        var after = await states.GetWorkingStateAsync(draftId, practice.BranchId);
        Assert.NotNull(after);
        Assert.Contains(after.ActiveSelections.Values, selection => selection.Source == PickSource.Simulation);
        Assert.Contains(after.ActiveSelections.Values, selection => selection.Source == PickSource.Keeper);

        var home = await mock.ReturnToLiveAsync(draftId);
        Assert.True(home.Succeeded, home.Error);
        var live = await states.GetWorkingStateAsync(draftId, home.BranchId);
        Assert.NotNull(live);
        Assert.True(live.ActiveBranch.ParentBranchId is null);
        Assert.Equal(DraftStatus.InProgress, live.Draft.Status);
    }

    [Fact]
    public async Task Switch_branch_keeps_independent_timelines()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var second = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        var third = PlayerId.FromName("Jahmyr Gibbs", "DET", "RB");
        var alternate = PlayerId.FromName("Ashton Jeanty", "LV", "RB");
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();
        var leagues = _services.GetRequiredService<ILeagueService>();

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, first))).Succeeded);
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, second))).Succeeded);
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, third))).Succeeded);

        var created = await commands.CreateBranchAsync(new CreateDraftBranchCommand(draftId, "What-if", 3));
        Assert.True(created.Succeeded, created.Error);
        Assert.NotNull(created.BranchId);
        var whatIf = created.BranchId.Value;

        var forked = await states.GetWorkingStateAsync(draftId, whatIf);
        Assert.NotNull(forked);
        Assert.Equal(2, forked.ActiveSelections.Count);
        Assert.False(forked.ActiveSelections.ContainsKey(3));

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, alternate))).Succeeded);

        var main = (await leagues.GetBranchesAsync(draftId)).Single(b => b.ParentBranchId is null);
        var toMain = await commands.SwitchBranchAsync(new SwitchDraftBranchCommand(draftId, main.BranchId));
        Assert.True(toMain.Succeeded, toMain.Error);

        var mainState = await states.GetWorkingStateAsync(draftId, main.BranchId);
        Assert.NotNull(mainState);
        Assert.Equal(main.BranchId, mainState.Draft.ActiveBranchId);
        Assert.Equal(third, mainState.ActiveSelections[3].PlayerId);

        var toWhatIf = await commands.SwitchBranchAsync(new SwitchDraftBranchCommand(draftId, whatIf));
        Assert.True(toWhatIf.Succeeded, toWhatIf.Error);

        var whatIfState = await states.GetWorkingStateAsync(draftId, whatIf);
        Assert.NotNull(whatIfState);
        Assert.Equal(whatIf, whatIfState.Draft.ActiveBranchId);
        Assert.Equal(alternate, whatIfState.ActiveSelections[3].PlayerId);
        Assert.False(whatIfState.UnavailablePlayers.Contains(third));
    }

    [Fact]
    public async Task Queue_can_be_reordered_and_items_removed()
    {
        var (draftId, first) = await CreateStartedDraftAsync();
        var second = PlayerId.FromName("Saquon Barkley", "PHI", "RB");
        var third = PlayerId.FromName("Jahmyr Gibbs", "DET", "RB");
        var states = _services.GetRequiredService<IDraftStateService>();
        var working = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(working);
        var branchId = working.ActiveBranch.BranchId;

        await states.AddToQueueAsync(new QueueAddCommand(draftId, first));
        await states.AddToQueueAsync(new QueueAddCommand(draftId, second));
        await states.AddToQueueAsync(new QueueAddCommand(draftId, third));
        await states.AddToQueueAsync(new QueueAddCommand(draftId, first));

        var queued = await states.GetQueueAsync(draftId, branchId);
        Assert.Equal([first, second, third], queued.Select(item => item.PlayerId));

        await states.ReorderQueueAsync(new QueueReorderCommand(draftId,
        [
            queued[2].QueueItemId,
            queued[0].QueueItemId,
            queued[1].QueueItemId
        ]));

        queued = await states.GetQueueAsync(draftId, branchId);
        Assert.Equal([third, first, second], queued.Select(item => item.PlayerId));

        await states.RemoveFromQueueAsync(new QueueRemoveCommand(draftId, queued[0].QueueItemId));
        queued = await states.GetQueueAsync(draftId, branchId);
        Assert.Equal([first, second], queued.Select(item => item.PlayerId));
    }

    [Fact]
    public async Task Draft_order_can_be_saved_before_start_and_is_locked_after()
    {
        var (draftId, _, firstTeam) = await CreateUnstartedDraftAsync();
        var leagues = _services.GetRequiredService<ILeagueService>();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();
        var working = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(working);
        var originalFirst = working.Slots[0].TeamId;
        Assert.Equal(firstTeam, originalFirst);

        var swapped = working.Teams
            .OrderBy(t => t.DraftPosition)
            .Select((team, index) => new TeamDraftPosition
            {
                TeamId = team.TeamId,
                Name = team.Name,
                OwnerName = team.OwnerName,
                DraftPosition = index == 0 ? 2 : index == 1 ? 1 : team.DraftPosition
            })
            .ToList();
        var newFirst = working.Teams.OrderBy(t => t.DraftPosition).ElementAt(1).TeamId;

        await leagues.SaveDraftOrderAsync(new SaveDraftOrderRequest
        {
            LeagueId = working.League.LeagueId,
            DraftId = draftId,
            DraftType = DraftType.Snake,
            Teams = swapped
        });

        var reordered = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(reordered);
        Assert.Equal(newFirst, reordered.Slots[0].TeamId);
        Assert.Equal(firstTeam, reordered.Slots[1].TeamId);
        var lastRound1 = reordered.Slots.Last(slot => slot.Round == 1);
        Assert.Equal(lastRound1.TeamId, reordered.Slots.First(slot => slot.Round == 2).TeamId);

        var started = await commands.StartDraftAsync(new StartDraftCommand(draftId));
        Assert.True(started.Succeeded, started.Error);

        var locked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            leagues.SaveDraftOrderAsync(new SaveDraftOrderRequest
            {
                LeagueId = working.League.LeagueId,
                DraftId = draftId,
                DraftType = DraftType.Linear,
                Teams = swapped
            }));
        Assert.Contains("before the draft starts", locked.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Saving_draft_order_rematches_keeper_slots()
    {
        var (draftId, player, firstTeam) = await CreateUnstartedDraftAsync();
        var leagues = _services.GetRequiredService<ILeagueService>();
        var states = _services.GetRequiredService<IDraftStateService>();

        await leagues.SaveKeepersAsync(new SaveKeepersRequest
        {
            DraftId = draftId,
            Keepers = [new KeeperSpec { TeamId = firstTeam, PlayerId = player, RoundCost = 2 }]
        });

        var before = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(before);
        var oldSlot = before.Slots.Single(slot => slot.IsKeeperSlot);
        Assert.Equal(2, oldSlot.Round);
        Assert.Equal(firstTeam, oldSlot.TeamId);

        var swapped = before.Teams
            .OrderBy(t => t.DraftPosition)
            .Select((team, index) => new TeamDraftPosition
            {
                TeamId = team.TeamId,
                Name = team.Name,
                DraftPosition = index == 0 ? 2 : index == 1 ? 1 : team.DraftPosition
            })
            .ToList();

        await leagues.SaveDraftOrderAsync(new SaveDraftOrderRequest
        {
            LeagueId = before.League.LeagueId,
            DraftId = draftId,
            DraftType = DraftType.Snake,
            Teams = swapped
        });

        var after = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(after);
        var keeperSlot = Assert.Single(after.Slots, slot => slot.IsKeeperSlot);
        Assert.Equal(firstTeam, keeperSlot.TeamId);
        Assert.Equal(2, keeperSlot.Round);
        Assert.NotEqual(oldSlot.DraftSlotId, keeperSlot.DraftSlotId);
    }

    [Fact]
    public async Task Draft_board_query_uses_player_names_not_ids()
    {
        var (draftId, player) = await CreateStartedDraftAsync();
        var commands = _services.GetRequiredService<IDraftCommandService>();
        var states = _services.GetRequiredService<IDraftStateService>();
        var queries = _services.GetRequiredService<IDraftQueryService>();

        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(draftId, player))).Succeeded);

        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var board = await queries.GetDraftBoardAsync(new QueryContext
        {
            DraftId = draftId,
            BranchId = state.ActiveBranch.BranchId
        });

        var pick = Assert.Single(board.Picks);
        Assert.Equal("Bijan Robinson", pick.Player);
        Assert.Equal("RB", pick.Position);
        Assert.Equal("ATL", pick.NflTeam);
        Assert.Equal("Manual", pick.Source);
        Assert.DoesNotContain(player.ToString(), pick.Player, StringComparison.OrdinalIgnoreCase);
        Assert.Contains('.', pick.RoundPick);
    }

    [Fact]
    public async Task Last_pick_marks_the_draft_complete()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Tiny",
            Season = 2026,
            TeamCount = 1,
            DraftType = DraftType.Linear,
            RoundCount = 1,
            UserTeamName = "Solo"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Tiny Draft"
        });
        var commands = _services.GetRequiredService<IDraftCommandService>();
        Assert.True((await commands.StartDraftAsync(new StartDraftCommand(draft.DraftId))).Succeeded);
        Assert.True((await commands.DraftPlayerAsync(new DraftPlayerCommand(
            draft.DraftId,
            PlayerId.FromName("Bijan Robinson", "ATL", "RB")))).Succeeded);

        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draft.DraftId);
        Assert.NotNull(state);
        Assert.Equal(DraftStatus.Completed, state.Draft.Status);
        var roster = await _services.GetRequiredService<IDraftQueryService>().GetTeamRosterAsync(
            new QueryContext { DraftId = draft.DraftId, BranchId = state.ActiveBranch.BranchId },
            league.UserTeamId!.Value);
        Assert.Equal("Bijan Robinson", Assert.Single(roster.Players).Name);
    }

    [Fact]
    public async Task Rankings_prefer_fantasypros_over_sleeper()
    {
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var playerId = PlayerId.FromName("Bijan Robinson", "ATL", "RB");
        var now = DateTimeOffset.UtcNow;
        var player = new Player
        {
            PlayerId = playerId,
            Name = "Bijan Robinson",
            NflTeam = "ATL",
            PrimaryPosition = PlayerPosition.RB,
            EligiblePositions = [PlayerPosition.RB]
        };

        await writer.WriteAsync("sleeper", [player], [],
        [
            new PlayerRanking { PlayerId = playerId, SourceKey = "sleeper", OverallRank = 4, CachedAt = now }
        ], [], [], CancellationToken.None);
        await writer.WriteAsync("fantasypros", [player], [],
        [
            new PlayerRanking { PlayerId = playerId, SourceKey = "fantasypros", OverallRank = 1, Tier = 1, CachedAt = now }
        ], [], [], CancellationToken.None);

        var preferred = await writer.GetRankingsAsync();
        Assert.Equal("fantasypros", preferred[playerId].SourceKey);
        Assert.Equal(1, preferred[playerId].OverallRank);
        Assert.Equal("sleeper", (await writer.GetRankingsAsync("sleeper"))[playerId].SourceKey);
    }

    [Fact]
    public async Task Provider_ids_round_trip_for_external_links()
    {
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var playerId = PlayerId.FromName("Bijan Robinson", "ATL", "RB");
        var player = new Player
        {
            PlayerId = playerId,
            Name = "Bijan Robinson",
            NflTeam = "ATL",
            PrimaryPosition = PlayerPosition.RB,
            EligiblePositions = [PlayerPosition.RB]
        };
        await writer.WriteAsync("sleeper", [player],
            [new PlayerProviderId { PlayerId = playerId, ProviderKey = "sleeper", ExternalId = "9226" }],
            [], [], [], CancellationToken.None);
        await writer.WriteAsync("fantasypros", [player],
            [
                new PlayerProviderId { PlayerId = playerId, ProviderKey = "fantasypros", ExternalId = "17298" },
                new PlayerProviderId { PlayerId = playerId, ProviderKey = "yahoo", ExternalId = "31002" }
            ],
            [], [], [], CancellationToken.None);

        var ids = await writer.GetProviderIdsAsync();
        Assert.Equal("9226", ids[playerId]["sleeper"]);
        Assert.Equal("31002", ids[playerId]["yahoo"]);
        Assert.Equal("17298", ids[playerId]["fantasypros"]);
    }

    [Fact]
    public async Task Rank_spread_round_trips_into_decision_context()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var playerId = PlayerId.FromName("Bijan Robinson", "ATL", "RB");
        var now = DateTimeOffset.UtcNow;
        await writer.WriteAsync("fantasypros",
            [
                new Player
                {
                    PlayerId = playerId,
                    Name = "Bijan Robinson",
                    NflTeam = "ATL",
                    PrimaryPosition = PlayerPosition.RB,
                    EligiblePositions = [PlayerPosition.RB]
                }
            ],
            [],
            [
                new PlayerRanking
                {
                    PlayerId = playerId,
                    SourceKey = "fantasypros-half",
                    OverallRank = 1,
                    RankMin = 1,
                    RankMax = 4,
                    RankStd = 0.8,
                    CachedAt = now
                }
            ],
            [],
            [],
            CancellationToken.None);

        var stored = await writer.GetRankingsAsync("fantasypros-half");
        Assert.Equal(1, stored[playerId].RankMin);
        Assert.Equal(4, stored[playerId].RankMax);
        Assert.Equal(0.8, stored[playerId].RankStd);

        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var context = await _services.GetRequiredService<IDraftQueryService>().GetDecisionContextAsync(
            new QueryContext { DraftId = draftId, BranchId = state.ActiveBranch.BranchId });
        var bijan = context.TopAvailable.First(player => player.Name == "Bijan Robinson");
        Assert.Equal(1, bijan.RankMin);
        Assert.Equal(4, bijan.RankMax);
        Assert.Equal(0.8, bijan.RankStd);
        Assert.Equal("1-4", bijan.RankRange);
    }

    [Fact]
    public async Task Scoring_can_be_saved_and_reloaded()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "PPR League",
            Season = 2026,
            TeamCount = 2,
            RoundCount = 4
        });
        await leagues.SaveScoringAsync(new SaveScoringRequest
        {
            LeagueId = league.LeagueId,
            Rules = ScoringCatalog.Ppr().Select(rule => new ScoringRuleSpec
            {
                Category = rule.Category,
                Points = rule.Points
            }).ToList()
        });

        var saved = await leagues.GetScoringRulesAsync(league.LeagueId);
        Assert.Equal(1m, saved.Single(rule => rule.Category == ScoringCategory.Reception).Points);
        Assert.Equal(4m, saved.Single(rule => rule.Category == ScoringCategory.PassingTouchdown).Points);
        var format = FantasyDataFormat.FromLeague(saved, await leagues.GetRosterSlotsAsync(league.LeagueId));
        Assert.Equal(ConsensusScoring.Ppr, format.Scoring);
        Assert.False(format.Superflex);
        Assert.Equal(5m, saved.Single(rule => rule.Category == ScoringCategory.FieldGoal50Plus).Points);
    }

    [Fact]
    public async Task Legacy_flat_field_goal_fills_short_bands_on_load()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Old FG",
            Season = 2026,
            TeamCount = 2,
            RoundCount = 4
        });
        await leagues.SaveScoringAsync(new SaveScoringRequest
        {
            LeagueId = league.LeagueId,
            Rules =
            [
                new ScoringRuleSpec { Category = ScoringCategory.Reception, Points = 0.5m },
                new ScoringRuleSpec { Category = ScoringCategory.FieldGoal, Points = 3m }
            ]
        });

        var saved = await leagues.GetScoringRulesAsync(league.LeagueId);
        Assert.Equal(3m, saved.Single(rule => rule.Category == ScoringCategory.FieldGoal0To19).Points);
        Assert.Equal(4m, saved.Single(rule => rule.Category == ScoringCategory.FieldGoal40To49).Points);
        Assert.Equal(5m, saved.Single(rule => rule.Category == ScoringCategory.FieldGoal50Plus).Points);
        Assert.DoesNotContain(saved, rule => rule.Category == ScoringCategory.FieldGoal);
    }

    [Fact]
    public async Task Draft_guidelines_round_trip_into_decision_context()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var leagues = _services.GetRequiredService<ILeagueService>();
        var states = _services.GetRequiredService<IDraftStateService>();
        var state = await states.GetWorkingStateAsync(draftId);
        Assert.NotNull(state);

        await leagues.SaveLeagueDetailsAsync(
            state.League.LeagueId,
            state.League.Name,
            state.League.Season,
            state.League.RoundCount,
            "Don't suggest a K or DEF until the last two rounds.");

        var saved = await leagues.GetLeagueAsync(state.League.LeagueId);
        Assert.Equal("Don't suggest a K or DEF until the last two rounds.", saved?.DraftGuidelines);

        var context = await _services.GetRequiredService<IDraftQueryService>().GetDecisionContextAsync(
            new QueryContext { DraftId = draftId, BranchId = state.ActiveBranch.BranchId });
        Assert.Equal("Don't suggest a K or DEF until the last two rounds.", context.League.DraftGuidelines);
    }

    [Fact]
    public async Task Decision_context_uses_one_qb_sheet_not_superflex()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        await WriteSplitRankingsAsync();

        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var context = await _services.GetRequiredService<IDraftQueryService>().GetDecisionContextAsync(
            new QueryContext { DraftId = draftId, BranchId = state.ActiveBranch.BranchId });

        Assert.Equal("Half PPR 1-QB", context.League.ScoringProfile);
        Assert.Null(context.League.DraftGuidelines);
        Assert.Equal("FantasyPros Half PPR 1-QB", context.RankingsSource);
        Assert.Contains(context.League.ScoringLines, line => line.StartsWith("Reception (PPR):", StringComparison.Ordinal));
        Assert.NotEmpty(context.MyRemainingNeeds);
        Assert.Equal("Bijan Robinson", context.TopAvailable[0].Name);
        Assert.Equal(1, context.TopAvailable[0].OverallRank);
    }

    [Fact]
    public async Task Decision_context_uses_superflex_sheet_for_superflex_league()
    {
        var leagues = _services.GetRequiredService<ILeagueService>();
        await _services.GetRequiredService<IFantasyDataProvider>()
            .RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Superflex Test",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            Superflex = true,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "SF Draft"
        });
        var start = await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new StartDraftCommand(draft.DraftId));
        Assert.True(start.Succeeded, start.Error);
        await WriteSplitRankingsAsync();

        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draft.DraftId);
        Assert.NotNull(state);
        var context = await _services.GetRequiredService<IDraftQueryService>().GetDecisionContextAsync(
            new QueryContext { DraftId = draft.DraftId, BranchId = state.ActiveBranch.BranchId });

        Assert.Equal("Half PPR Superflex", context.League.ScoringProfile);
        Assert.Equal("FantasyPros Half PPR Superflex", context.RankingsSource);
        Assert.Equal("Josh Allen", context.TopAvailable[0].Name);
        Assert.Equal(1, context.TopAvailable[0].OverallRank);
    }

    [Fact]
    public async Task Ai_conversation_persists_for_a_draft_branch()
    {
        var (draftId, _) = await CreateStartedDraftAsync();
        var state = await _services.GetRequiredService<IDraftStateService>().GetWorkingStateAsync(draftId);
        Assert.NotNull(state);
        var store = _services.GetRequiredService<IAiResponseStore>();
        await store.SaveAsync(new AiSavedResponse
        {
            ResponseId = Guid.NewGuid().ToString("D"),
            DraftId = draftId,
            BranchId = state.ActiveBranch.BranchId,
            Provider = "xai",
            Model = "grok-4.6",
            AnalyzedStateVersion = 1,
            Prompt = "Who should I take here?",
            Body = "Recommendation: Bijan Robinson",
            RequestStartedAt = DateTimeOffset.UtcNow,
            ResponseCompletedAt = DateTimeOffset.UtcNow
        });

        var loaded = await store.ListAsync(draftId, state.ActiveBranch.BranchId);
        var turn = Assert.Single(loaded);
        Assert.Equal("Who should I take here?", turn.Prompt);
        Assert.Equal("Recommendation: Bijan Robinson", turn.Body);
        Assert.Equal("xai", turn.Provider);
    }

    [Fact]
    public async Task Player_years_exp_survives_reload()
    {
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var playerId = PlayerId.FromName("First Year", "SEA", "WR");
        await writer.WriteAsync("sleeper",
        [
            new Player
            {
                PlayerId = playerId,
                Name = "First Year",
                NflTeam = "SEA",
                PrimaryPosition = PlayerPosition.WR,
                EligiblePositions = [PlayerPosition.WR],
                YearsExp = 0
            }
        ], [], [], [], [], CancellationToken.None);

        var loaded = (await _services.GetRequiredService<IDraftStateService>().GetPlayersAsync())
            .Single(player => player.PlayerId.Equals(playerId));
        Assert.Equal(0, loaded.YearsExp);
        Assert.True(loaded.IsRookie);
    }

    [Fact]
    public async Task Injury_notes_survive_a_refresh_that_omits_them()
    {
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var playerId = PlayerId.FromName("Banged Up", "KC", "WR");
        var player = new Player
        {
            PlayerId = playerId,
            Name = "Banged Up",
            NflTeam = "KC",
            PrimaryPosition = PlayerPosition.WR,
            EligiblePositions = [PlayerPosition.WR],
            Status = PlayerStatus.Questionable,
            InjuryBodyPart = "Hamstring",
            InjuryNotes = "Limited Wednesday"
        };
        await writer.WriteAsync("sleeper", [player], [], [], [], [], CancellationToken.None);
        await writer.WriteAsync("fantasypros",
        [
            new Player
            {
                PlayerId = playerId,
                Name = "Banged Up",
                NflTeam = "KC",
                PrimaryPosition = PlayerPosition.WR,
                EligiblePositions = [PlayerPosition.WR],
                Status = PlayerStatus.Questionable
            }
        ], [], [], [], [], CancellationToken.None);

        var loaded = (await _services.GetRequiredService<IDraftStateService>().GetPlayersAsync())
            .Single(row => row.PlayerId.Equals(playerId));
        Assert.Equal("Hamstring", loaded.InjuryBodyPart);
        Assert.Equal("Limited Wednesday", loaded.InjuryNotes);
    }

    [Fact]
    public async Task Seed_provider_writes_cache()
    {
        var provider = _services.GetRequiredService<IFantasyDataProvider>();
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var result = await provider.RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);
        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.PlayersWritten > 50);

        var rankings = await writer.GetRankingsAsync();
        var adp = await writer.GetAdpAsync();
        Assert.NotEmpty(rankings);
        Assert.NotEmpty(adp);
    }

    private async Task<(DraftId DraftId, PlayerId FirstPlayer)> CreateStartedDraftAsync()
    {
        var (draftId, player, _) = await CreateUnstartedDraftAsync();
        var start = await _services.GetRequiredService<IDraftCommandService>()
            .StartDraftAsync(new StartDraftCommand(draftId));
        Assert.True(start.Succeeded, start.Error);
        return (draftId, player);
    }

    private async Task<(DraftId DraftId, PlayerId FirstPlayer, TeamId FirstTeam)> CreateUnstartedDraftAsync()
    {
        var seed = _services.GetRequiredService<IFantasyDataProvider>();
        await seed.RefreshAsync(new FantasyDraftAssistant.Core.Results.FantasyDataRefreshRequest(), CancellationToken.None);

        var leagues = _services.GetRequiredService<ILeagueService>();
        var league = await leagues.CreateLeagueAsync(new CreateLeagueRequest
        {
            Name = "Test League",
            Season = 2026,
            TeamCount = 4,
            DraftType = DraftType.Snake,
            RoundCount = 4,
            UserTeamName = "My Team"
        });
        var draft = await leagues.CreateDraftAsync(new CreateDraftRequest
        {
            LeagueId = league.LeagueId,
            Name = "Test Draft"
        });
        var teams = await leagues.GetTeamsAsync(league.LeagueId);
        return (draft.DraftId, PlayerId.FromName("Bijan Robinson", "ATL", "RB"), teams[0].TeamId);
    }

    private async Task WriteSplitRankingsAsync()
    {
        var writer = _services.GetRequiredService<IFantasyDataWriter>();
        var bijan = PlayerId.FromName("Bijan Robinson", "ATL", "RB");
        var allen = PlayerId.FromName("Josh Allen", "BUF", "QB");
        var now = DateTimeOffset.UtcNow;
        Player Player(PlayerId id, string name, string team, PlayerPosition position) => new()
        {
            PlayerId = id,
            Name = name,
            NflTeam = team,
            PrimaryPosition = position,
            EligiblePositions = [position]
        };
        PlayerRanking Rank(PlayerId id, string source, int rank) => new()
        {
            PlayerId = id,
            SourceKey = source,
            OverallRank = rank,
            CachedAt = now
        };

        await writer.WriteAsync("fantasypros",
            [Player(bijan, "Bijan Robinson", "ATL", PlayerPosition.RB), Player(allen, "Josh Allen", "BUF", PlayerPosition.QB)],
            [],
            [
                Rank(bijan, "fantasypros-half", 1),
                Rank(allen, "fantasypros-half", 12),
                Rank(allen, "fantasypros-half-sf", 1),
                Rank(bijan, "fantasypros-half-sf", 8),
                Rank(allen, "fantasypros", 1),
                Rank(bijan, "fantasypros", 8)
            ],
            [],
            [],
            CancellationToken.None);
    }

    public void Dispose()
    {
        _services.Dispose();
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, true);
        }
        catch (IOException)
        {
        }
    }
}
