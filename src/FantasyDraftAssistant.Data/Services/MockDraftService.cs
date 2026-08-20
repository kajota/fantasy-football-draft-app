using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Interfaces;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Data.Database;

namespace FantasyDraftAssistant.Data.Services;

public sealed class MockDraftService(
    SqliteConnectionFactory factory,
    IDraftCommandService commands,
    IDraftStateService drafts,
    ILeagueService leagues,
    IFantasyDataWriter fantasyData) : IMockDraftService
{
    public Task<IReadOnlyList<MockSeatPolicy>> GetPoliciesAsync(
        DraftId draftId,
        BranchId branchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            SELECT TeamId, Personality, IsCpu
            FROM MockSeatPolicies
            WHERE DraftId = $d AND BranchId = $b;
            """)
            .Bind("$d", draftId.ToString())
            .Bind("$b", branchId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<MockSeatPolicy>();
        while (reader.Read())
        {
            list.Add(new MockSeatPolicy
            {
                TeamId = TeamId.Parse(reader.GetString(0)),
                Personality = Enum.Parse<MockPersonality>(reader.GetString(1)),
                IsCpu = reader.GetInt32(2) == 1
            });
        }

        return Task.FromResult<IReadOnlyList<MockSeatPolicy>>(list);
    }

    public async Task<IReadOnlyList<PredictedPick>> PredictUpcomingPicksAsync(
        DraftId draftId,
        BranchId? branchId = null,
        CancellationToken cancellationToken = default)
    {
        // A private load, mutated by the simulation and discarded. Nothing is persisted.
        var scratch = await drafts.GetWorkingStateAsync(draftId, branchId, cancellationToken);
        if (scratch?.CurrentSlot is null)
            return [];

        var players = await drafts.GetPlayersAsync(cancellationToken);
        var (rankings, adp) = await LoadBoardDataAsync(scratch, cancellationToken);
        var policies = await GetPoliciesAsync(draftId, scratch.ActiveBranch.BranchId, cancellationToken);
        var byTeam = policies.ToDictionary(policy => policy.TeamId, policy => policy.Personality);

        return DraftForecast.SimulateOnto(
            scratch,
            players,
            rankings,
            adp,
            teamId => byTeam.GetValueOrDefault(teamId, MockPersonality.BestAvailable),
            scratch.League.UserTeamId);
    }

    public async Task SeedPoliciesAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default)
    {
        var state = await drafts.GetWorkingStateAsync(draftId, branchId, cancellationToken)
                    ?? throw new InvalidOperationException("Draft not found.");
        var seed = HashCode.Combine(draftId.Value, branchId.Value);
        var policies = MockPersonalityCatalog.Assign(state.Teams, state.League.UserTeamId, seed);
        SavePolicies(draftId, branchId, policies);
    }

    public async Task<MockPickResult> StartPracticeAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        var state = await drafts.GetWorkingStateAsync(draftId, cancellationToken: cancellationToken);
        if (state is null)
            return MockPickResult.Fail("Draft not found.");

        if (state.Draft.Status == DraftStatus.NotStarted)
        {
            var started = await commands.StartDraftAsync(new StartDraftCommand(draftId), cancellationToken);
            if (!started.Succeeded)
                return MockPickResult.Fail(started.Error ?? "Could not start the draft.");
            state = started.State
                    ?? await drafts.GetWorkingStateAsync(draftId, cancellationToken: cancellationToken);
            if (state is null)
                return MockPickResult.Fail("Draft not found after start.");
        }

        var live = await LiveBranchAsync(draftId, cancellationToken);
        if (live is not null && !state.ActiveBranch.BranchId.Equals(live.BranchId))
        {
            var switched = await commands.SwitchBranchAsync(new SwitchDraftBranchCommand(draftId, live.BranchId), cancellationToken);
            if (!switched.Succeeded)
                return MockPickResult.Fail(switched.Error ?? "Could not return to the live draft.");
            state = await drafts.GetWorkingStateAsync(draftId, live.BranchId, cancellationToken)
                    ?? state;
        }

        if (state.CurrentSlot is null)
            return MockPickResult.Fail("The live draft is already complete. There is no open pick to practice from.");

        var slot = state.CurrentSlot;
        var name = $"Practice from {DraftSlotGenerator.FormatRoundPick(slot.Round, slot.RoundPick)}";
        var created = await commands.CreateBranchAsync(
            new CreateDraftBranchCommand(draftId, name, slot.OverallPick),
            cancellationToken);
        if (!created.Succeeded)
            return MockPickResult.Fail(created.Error ?? "Could not create a practice branch.");

        var branchId = created.BranchId ?? created.State?.ActiveBranch.BranchId
                       ?? throw new InvalidOperationException("Practice branch id missing.");
        await SeedPoliciesAsync(draftId, branchId, cancellationToken);
        return new MockPickResult
        {
            Succeeded = true,
            BranchId = branchId
        };
    }

    public async Task<MockPickResult> ReturnToLiveAsync(DraftId draftId, CancellationToken cancellationToken = default)
    {
        var live = await LiveBranchAsync(draftId, cancellationToken);
        if (live is null)
            return MockPickResult.Fail("No live draft timeline found.");

        var switched = await commands.SwitchBranchAsync(new SwitchDraftBranchCommand(draftId, live.BranchId), cancellationToken);
        if (!switched.Succeeded)
            return MockPickResult.Fail(switched.Error ?? "Could not return to the live draft.");

        return new MockPickResult
        {
            Succeeded = true,
            BranchId = live.BranchId
        };
    }

    public async Task<MockPickResult> SimulateNextAsync(
        DraftId draftId,
        BranchId? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var state = await drafts.GetWorkingStateAsync(draftId, branchId, cancellationToken);
        if (state is null)
            return MockPickResult.Fail("Draft not found.");
        if (state.CurrentSlot is null)
            return MockPickResult.Complete();

        var slot = state.CurrentSlot;
        var team = state.Teams.FirstOrDefault(item => item.TeamId.Equals(slot.TeamId));
        var policies = await GetPoliciesAsync(draftId, state.ActiveBranch.BranchId, cancellationToken);
        var policy = policies.FirstOrDefault(item => item.TeamId.Equals(slot.TeamId));
        var isUser = state.League.UserTeamId is { } user && slot.TeamId.Equals(user);
        if (isUser || policy is { IsCpu: false })
            return MockPickResult.UserPick();

        var personality = policy?.Personality ?? MockPersonality.BestAvailable;
        var players = await drafts.GetPlayersAsync(cancellationToken);
        var (rankings, adp) = await LoadBoardDataAsync(state, cancellationToken);
        var playerId = MockPickPolicy.Choose(state, players, rankings, adp, personality);
        if (playerId is null)
            return MockPickResult.Fail("No available player left for the CPU.");

        var drafted = await commands.DraftPlayerAsync(
            new DraftPlayerCommand(draftId, playerId.Value, PickSource.Simulation),
            cancellationToken);
        if (!drafted.Succeeded)
            return MockPickResult.Fail(drafted.Error ?? "CPU pick failed.");

        var player = players.FirstOrDefault(item => item.PlayerId.Equals(playerId.Value));
        return new MockPickResult
        {
            Succeeded = true,
            PicksMade = 1,
            PlayerName = player?.Name ?? playerId.Value.ToString(),
            TeamName = team?.Label ?? "CPU",
            Personality = MockPersonalityCatalog.Title(personality),
            BranchId = state.ActiveBranch.BranchId
        };
    }

    private async Task<DraftBranch?> LiveBranchAsync(DraftId draftId, CancellationToken cancellationToken)
    {
        var branches = await leagues.GetBranchesAsync(draftId, cancellationToken);
        return branches.FirstOrDefault(branch => branch.ParentBranchId is null)
               ?? branches.OrderBy(branch => branch.CreatedAt).FirstOrDefault();
    }

    private void SavePolicies(DraftId draftId, BranchId branchId, IReadOnlyList<MockSeatPolicy> policies)
    {
        using var db = factory.Open();
        using var tx = db.BeginTransaction();
        using (var clear = db.Cmd("DELETE FROM MockSeatPolicies WHERE DraftId = $d AND BranchId = $b;", tx)
                   .Bind("$d", draftId.ToString())
                   .Bind("$b", branchId.ToString()))
        {
            clear.ExecuteNonQuery();
        }

        foreach (var policy in policies)
        {
            using var cmd = db.Cmd("""
                INSERT INTO MockSeatPolicies(DraftId, BranchId, TeamId, Personality, IsCpu)
                VALUES ($d, $b, $t, $p, $cpu);
                """, tx)
                .Bind("$d", draftId.ToString())
                .Bind("$b", branchId.ToString())
                .Bind("$t", policy.TeamId.ToString())
                .Bind("$p", policy.Personality.ToString())
                .Bind("$cpu", policy.IsCpu ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        tx.Commit();
    }

    private async Task<(
        IReadOnlyDictionary<PlayerId, PlayerRanking> Rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> Adp)> LoadBoardDataAsync(
        FantasyDraftAssistant.Core.Results.DraftWorkingState state,
        CancellationToken cancellationToken)
    {
        var format = FantasyDataFormat.FromLeague(state.ScoringRules, state.RosterSlots);
        var sourceKey = FantasyDataSourcePicker.Pick(await fantasyData.GetSourceKeysAsync(cancellationToken), format);
        var rankings = await fantasyData.GetRankingsAsync(sourceKey, cancellationToken);
        var adp = await fantasyData.GetAdpAsync(sourceKey, cancellationToken);
        if (rankings.Count == 0)
            rankings = await fantasyData.GetRankingsAsync(cancellationToken: cancellationToken);
        if (adp.Count == 0)
            adp = await fantasyData.GetAdpAsync(cancellationToken: cancellationToken);
        return (rankings, adp);
    }
}
