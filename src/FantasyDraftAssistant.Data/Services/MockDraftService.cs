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
    IFantasyDataWriter fantasyData,
    IMockPickAdvisor? advisor = null) : IMockDraftService
{
    /// How many players the model gets to choose from. Matches the shortlist size the
    /// Ask path already uses, and bounds the prompt so a pick stays cheap.
    private const int AiCandidateCount = 24;

    private const int RecentPickCount = 12;

    public Task<IReadOnlyList<MockSeatPolicy>> GetPoliciesAsync(
        DraftId draftId,
        BranchId branchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            SELECT TeamId, Personality, IsCpu, AiModel, AiStrategy
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
                IsCpu = reader.GetInt32(2) == 1,
                AiModel = reader.IsDBNull(3) ? null : reader.GetString(3),
                AiStrategy = reader.IsDBNull(4) ? null : reader.GetString(4)
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
        var strategyByTeam = policies
            .Where(policy => policy.AiStrategy is not null)
            .ToDictionary(policy => policy.TeamId, policy => policy.AiStrategy);

        return DraftForecast.SimulateOnto(
            scratch,
            players,
            rankings,
            adp,
            // An Ai seat is stood in for by its strategy's deterministic proxy. The
            // outlook simulates up to 32 picks on every refresh; routing that through a
            // provider would fan out dozens of paid calls per keystroke.
            teamId => ProxyFor(byTeam.GetValueOrDefault(teamId, MockPersonality.BestAvailable),
                               strategyByTeam.GetValueOrDefault(teamId)),
            scratch.League.UserTeamId);
    }

    public async Task SeedPoliciesAsync(DraftId draftId, BranchId branchId, CancellationToken cancellationToken = default)
    {
        var state = await drafts.GetWorkingStateAsync(draftId, branchId, cancellationToken)
                    ?? throw new InvalidOperationException("Draft not found.");
        var seed = HashCode.Combine(draftId.Value, branchId.Value);
        var policies = MockPersonalityCatalog.Assign(state.Teams, state.League.UserTeamId, seed, draftId, branchId);
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

        MockAiPick? aiPick = null;
        var fallbackPersonality = personality;
        if (personality == MockPersonality.Ai)
        {
            var strategy = MockAiStrategyCatalog.Find(policy?.AiStrategy);
            // Whatever happens below, this seat still drafts. The proxy is what it
            // falls back to, and what the turn outlook already assumes it will do.
            fallbackPersonality = strategy.Proxy;
            if (advisor is not null && !string.IsNullOrWhiteSpace(policy?.AiModel))
            {
                aiPick = await TryAiPickAsync(
                    state, slot, team, players, rankings, adp, policy!.AiModel!, strategy, cancellationToken);
            }
        }

        var playerId = aiPick?.PlayerId
                       ?? MockPickPolicy.Choose(state, players, rankings, adp, fallbackPersonality);
        if (playerId is null)
            return MockPickResult.Fail("No available player left for the CPU.");

        var drafted = await commands.DraftPlayerAsync(
            new DraftPlayerCommand(draftId, playerId.Value, PickSource.Simulation),
            cancellationToken);
        if (!drafted.Succeeded)
            return MockPickResult.Fail(drafted.Error ?? "CPU pick failed.");

        var player = players.FirstOrDefault(item => item.PlayerId.Equals(playerId.Value));

        if (personality == MockPersonality.Ai)
        {
            SaveReason(
                draftId,
                state.ActiveBranch.BranchId,
                slot.OverallPick,
                slot.TeamId,
                aiPick,
                policy?.AiStrategy,
                policy?.AiModel);
        }

        return new MockPickResult
        {
            Succeeded = true,
            PicksMade = 1,
            PlayerName = player?.Name ?? playerId.Value.ToString(),
            TeamName = team?.Label ?? "CPU",
            Personality = MockPersonalityCatalog.Title(personality),
            Reason = aiPick?.Reason,
            BranchId = state.ActiveBranch.BranchId
        };
    }

    private async Task<DraftBranch?> LiveBranchAsync(DraftId draftId, CancellationToken cancellationToken)
    {
        var branches = await leagues.GetBranchesAsync(draftId, cancellationToken);
        return branches.FirstOrDefault(branch => branch.ParentBranchId is null)
               ?? branches.OrderBy(branch => branch.CreatedAt).FirstOrDefault();
    }

    private static MockPersonality ProxyFor(MockPersonality personality, string? strategyKey) =>
        personality == MockPersonality.Ai
            ? MockAiStrategyCatalog.Find(strategyKey).Proxy
            : personality;

    /// Returns null on every failure path - no model, a timeout, an unparseable answer,
    /// a player that is not actually available. The caller falls back and drafts on.
    private async Task<MockAiPick?> TryAiPickAsync(
        FantasyDraftAssistant.Core.Results.DraftWorkingState state,
        DraftSlot slot,
        Team? team,
        IReadOnlyList<Player> players,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        string aiModel,
        MockAiStrategy strategy,
        CancellationToken cancellationToken)
    {
        var byId = players.ToDictionary(player => player.PlayerId);

        var candidates = players
            .Where(player => !state.UnavailablePlayers.Contains(player.PlayerId))
            .OrderBy(player => rankings.TryGetValue(player.PlayerId, out var rank) ? rank.OverallRank : 400)
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .Take(AiCandidateCount)
            .Select(player => new MockAiCandidate(
                player.PlayerId,
                player.Name,
                player.PrimaryPosition.ToString(),
                player.NflTeam,
                rankings.TryGetValue(player.PlayerId, out var rank) ? rank.OverallRank : 400,
                adp.TryGetValue(player.PlayerId, out var adpRow) ? adpRow.OverallAdp : null,
                player.YearsExp ?? 0))
            .ToList();
        if (candidates.Count == 0)
            return null;

        var drafted = state.SelectionsForTeam(slot.TeamId)
            .Select(selection => byId.GetValueOrDefault(selection.PlayerId))
            .Where(player => player is not null)
            .Select(player => $"{player!.Name} ({player.PrimaryPosition})")
            .ToList();

        var draftedPositions = state.SelectionsForTeam(slot.TeamId)
            .Select(selection => byId.GetValueOrDefault(selection.PlayerId)?.PrimaryPosition)
            .Where(position => position.HasValue)
            .Select(position => position!.Value)
            .ToList();

        var needs = RosterRules.RemainingNeeds(state.RosterSlots, draftedPositions)
            .Where(need => need.Value > 0)
            .Select(need => $"{need.Key} x{need.Value}")
            .ToList();

        var recent = state.ActiveSelections.Values
            .OrderByDescending(selection => selection.OverallPick)
            .Take(RecentPickCount)
            .Select(selection =>
            {
                var picked = byId.GetValueOrDefault(selection.PlayerId);
                var by = state.Teams.FirstOrDefault(item => item.TeamId.Equals(selection.TeamId));
                // Mark the seat's own picks explicitly. Left to infer it from team
                // names, a model claimed credit for a rival's first-rounder.
                var mine = selection.TeamId.Equals(slot.TeamId) ? " [YOURS]" : "";
                return $"{selection.OverallPick}. {picked?.Name ?? "?"} ({picked?.PrimaryPosition}) - {by?.Label ?? "?"}{mine}";
            })
            .ToList();

        MockAiPick? pick;
        try
        {
            pick = await advisor!.ChooseAsync(new MockAiPickRequest
            {
                DraftId = state.Draft.DraftId,
                TeamId = slot.TeamId,
                AiModel = aiModel,
                StrategyPrompt = strategy.PromptLine,
                Round = slot.Round,
                RoundPick = slot.RoundPick,
                RoundCount = state.League.RoundCount,
                TeamName = team?.Label ?? "this team",
                Candidates = candidates,
                RosterSoFar = drafted,
                RemainingNeeds = needs,
                RecentPicks = recent,
                ScoringSummary = ScoringSummary(state)
            }, cancellationToken);
        }
        catch (Exception)
        {
            // An advisor is expected to answer with null rather than throw, but a
            // practice draft must not stall on one that misbehaves.
            return null;
        }

        // Belt and braces: the advisor only offers shortlist entries, but the shortlist
        // is built from a snapshot and a pick must never be an unavailable player.
        if (pick is null || state.UnavailablePlayers.Contains(pick.PlayerId))
            return null;

        return pick;
    }

    private static string ScoringSummary(FantasyDraftAssistant.Core.Results.DraftWorkingState state)
    {
        var ppr = state.ScoringRules.FirstOrDefault(rule => rule.Category == ScoringCategory.Reception)?.Points ?? 0m;
        var passTd = state.ScoringRules.FirstOrDefault(rule => rule.Category == ScoringCategory.PassingTouchdown)?.Points ?? 4m;
        var superflex = state.RosterSlots.Any(slot => slot.SlotCode == "Q/W/R/T");
        var format = ppr switch
        {
            >= 1m => "full PPR",
            >= 0.5m => "half PPR",
            > 0m => $"{ppr} per reception",
            _ => "standard (no PPR)"
        };

        // Always state the QB format. Saying nothing for a 1-QB league let a model
        // read "especially in Superflex" out of its strategy and take a quarterback
        // second overall in a league that starts one.
        var qbFormat = superflex
            ? "Superflex (a second QB can start in the flex)"
            : "1-QB (only one quarterback starts, so QBs are worth much less)";
        return $"{format}, {passTd} point passing TDs, {qbFormat}.";
    }

    private void SaveReason(
        DraftId draftId,
        BranchId branchId,
        int overallPick,
        TeamId teamId,
        MockAiPick? pick,
        string? strategyKey,
        string? configuredModel)
    {
        var strategy = MockAiStrategyCatalog.Find(strategyKey);
        var usedFallback = pick is null;
        var (provider, model) = configuredModel is null
            ? ("", "")
            : (configuredModel.Split(':')[0], configuredModel);

        using var db = factory.Open();
        using var cmd = db.Cmd("""
            INSERT INTO MockPickReasons(DraftId, BranchId, OverallPick, TeamId, Provider, Model, Strategy, Reason, UsedFallback, CreatedAt)
            VALUES ($d, $b, $pick, $t, $provider, $model, $strategy, $reason, $fallback, $at)
            ON CONFLICT(DraftId, BranchId, OverallPick) DO UPDATE SET
                TeamId = excluded.TeamId,
                Provider = excluded.Provider,
                Model = excluded.Model,
                Strategy = excluded.Strategy,
                Reason = excluded.Reason,
                UsedFallback = excluded.UsedFallback,
                CreatedAt = excluded.CreatedAt;
            """)
            .Bind("$d", draftId.ToString())
            .Bind("$b", branchId.ToString())
            .Bind("$pick", overallPick)
            .Bind("$t", teamId.ToString())
            .Bind("$provider", pick?.Provider ?? provider)
            .Bind("$model", pick?.Model ?? model)
            .Bind("$strategy", strategy.Key)
            .Bind("$reason", pick?.Reason ?? "The model did not answer, so the deterministic policy made this pick.")
            .Bind("$fallback", usedFallback ? 1 : 0)
            .Bind("$at", DateTimeOffset.UtcNow.ToString("O"));
        cmd.ExecuteNonQuery();
    }

    public Task<IReadOnlyList<MockPickReason>> GetPickReasonsAsync(
        DraftId draftId,
        BranchId branchId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var db = factory.Open();
        using var cmd = db.Cmd("""
            SELECT OverallPick, TeamId, Provider, Model, Strategy, Reason, UsedFallback
            FROM MockPickReasons
            WHERE DraftId = $d AND BranchId = $b
            ORDER BY OverallPick;
            """)
            .Bind("$d", draftId.ToString())
            .Bind("$b", branchId.ToString());
        using var reader = cmd.ExecuteReader();
        var list = new List<MockPickReason>();
        while (reader.Read())
        {
            list.Add(new MockPickReason
            {
                OverallPick = reader.GetInt32(0),
                TeamId = TeamId.Parse(reader.GetString(1)),
                Provider = reader.GetString(2),
                Model = reader.GetString(3),
                Strategy = reader.IsDBNull(4) ? null : reader.GetString(4),
                Reason = reader.GetString(5),
                UsedFallback = reader.GetInt32(6) == 1
            });
        }

        return Task.FromResult<IReadOnlyList<MockPickReason>>(list);
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
                INSERT INTO MockSeatPolicies(DraftId, BranchId, TeamId, Personality, IsCpu, AiModel, AiStrategy)
                VALUES ($d, $b, $t, $p, $cpu, $model, $strategy);
                """, tx)
                .Bind("$d", draftId.ToString())
                .Bind("$b", branchId.ToString())
                .Bind("$t", policy.TeamId.ToString())
                .Bind("$p", policy.Personality.ToString())
                .Bind("$cpu", policy.IsCpu ? 1 : 0)
                .Bind("$model", policy.AiModel)
                .Bind("$strategy", policy.AiStrategy);
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
