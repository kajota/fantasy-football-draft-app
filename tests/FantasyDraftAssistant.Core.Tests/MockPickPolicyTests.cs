using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Tests;

public class MockPickPolicyTests
{
    [Fact]
    public void Best_available_takes_the_top_rank()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.RB, "Bijan", 1),
            P(PlayerPosition.WR, "Chase", 2),
            P(PlayerPosition.QB, "Allen", 3));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.BestAvailable);
        Assert.Equal(players[0].PlayerId, pick);
    }

    [Fact]
    public void Zero_rb_skips_the_top_running_back_early()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.RB, "Bijan", 1),
            P(PlayerPosition.WR, "Chase", 3));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.ZeroRb);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Rb_first_takes_the_running_back_over_a_slightly_better_receiver()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.WR, "Chase", 1),
            P(PlayerPosition.RB, "Bijan", 4));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.RbFirst);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Late_qb_does_not_take_a_quarterback_in_round_one()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.QB, "Allen", 1),
            P(PlayerPosition.WR, "Chase", 5));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.LateQb);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Superflex_qb_early_takes_a_nearby_quarterback()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.WR, "Chase", 1),
            P(PlayerPosition.QB, "Allen", 5));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.QbEarly);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Rookie_hunter_prefers_the_rookie()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.WR, "Adams", 2),
            P(PlayerPosition.WR, "Hunter", 8, yearsExp: 0));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.RookieHunter);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Does_not_take_a_kicker_in_round_one()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.K, "Tucker", 1),
            P(PlayerPosition.RB, "Bijan", 12));

        var pick = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.BestAvailable);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Last_rounds_fill_kicker_and_defense_over_extra_skill()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.WR, "Leftover", 140),
            P(PlayerPosition.K, "Tucker", 180),
            P(PlayerPosition.DEF, "Ravens", 190));
        FillThroughRound(state, 14);

        var first = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.BestAvailable);
        Assert.Equal(players[1].PlayerId, first);

        var teamId = state.CurrentSlot!.TeamId;
        PickCurrent(state, first!.Value);
        while (state.CurrentSlot is { } slot && !slot.TeamId.Equals(teamId))
            PickCurrent(state, PlayerId.FromName($"Skip{slot.OverallPick}", "WR"));

        var second = MockPickPolicy.Choose(state, players, rankings, None, MockPersonality.BestAvailable);
        Assert.Equal(players[2].PlayerId, second);
    }

    private static void FillThroughRound(DraftWorkingState state, int lastFilledRound)
    {
        var lastOverall = lastFilledRound * state.League.TeamCount;
        foreach (var slot in state.Slots.Where(item => item.OverallPick <= lastOverall))
            PickSlot(state, slot, PlayerId.FromName($"Filler{slot.OverallPick}", "WR"));
    }

    private static void PickCurrent(DraftWorkingState state, PlayerId playerId)
    {
        var slot = state.CurrentSlot ?? throw new InvalidOperationException("No open slot.");
        PickSlot(state, slot, playerId);
    }

    private static void PickSlot(DraftWorkingState state, DraftSlot slot, PlayerId playerId)
    {
        state.ActiveSelections[slot.OverallPick] = new ActiveSelection
        {
            EventId = EventId.New(),
            DraftId = state.Draft.DraftId,
            BranchId = state.ActiveBranch.BranchId,
            DraftSlotId = slot.DraftSlotId,
            OverallPick = slot.OverallPick,
            Round = slot.Round,
            RoundPick = slot.RoundPick,
            TeamId = slot.TeamId,
            PlayerId = playerId,
            Source = PickSource.Simulation,
            ObservedAt = DateTimeOffset.UtcNow
        };
        state.UnavailablePlayers.Add(playerId);
    }

    [Fact]
    public void Adp_hunter_follows_adp_not_rank()
    {
        var (state, players, rankings) = Board(
            P(PlayerPosition.WR, "Chase", 1),
            P(PlayerPosition.RB, "Bijan", 20));
        var adp = new Dictionary<PlayerId, PlayerAdp>
        {
            [players[0].PlayerId] = new() { PlayerId = players[0].PlayerId, SourceKey = "seed", OverallAdp = 8 },
            [players[1].PlayerId] = new() { PlayerId = players[1].PlayerId, SourceKey = "seed", OverallAdp = 1.2 }
        };

        var pick = MockPickPolicy.Choose(state, players, rankings, adp, MockPersonality.AdpHunter);
        Assert.Equal(players[1].PlayerId, pick);
    }

    [Fact]
    public void Deal_covers_every_cpu_seat()
    {
        var dealt = MockPersonalityCatalog.Deal(11, seed: 7);
        Assert.Equal(11, dealt.Count);
        Assert.Contains(MockPersonality.ZeroRb, dealt);
    }

    private static readonly Dictionary<PlayerId, PlayerAdp> None = [];

    private static (DraftWorkingState State, List<Player> Players, Dictionary<PlayerId, PlayerRanking> Rankings) Board(
        params (Player Player, int Rank)[] entries)
    {
        var state = LeagueFactory.CreateStandardState(teamCount: 4, roundCount: 16);
        var start = DraftEngine.StartDraft(state, new StartDraftCommand(state.Draft.DraftId));
        Assert.True(start.Succeeded, start.Error);
        var players = entries.Select(entry => entry.Player).ToList();
        var rankings = entries.ToDictionary(
            entry => entry.Player.PlayerId,
            entry => new PlayerRanking
            {
                PlayerId = entry.Player.PlayerId,
                SourceKey = "test",
                OverallRank = entry.Rank
            });
        return (state, players, rankings);
    }

    private static (Player Player, int Rank) P(PlayerPosition position, string name, int rank, int yearsExp = 4)
    {
        var nfl = position switch
        {
            PlayerPosition.RB => "ATL",
            PlayerPosition.WR => "CIN",
            PlayerPosition.QB => "BUF",
            PlayerPosition.K => "BAL",
            _ => "FA"
        };
        var player = new Player
        {
            PlayerId = PlayerId.FromName(name, position.ToString()),
            Name = name,
            NflTeam = nfl,
            PrimaryPosition = position,
            EligiblePositions = [position],
            YearsExp = yearsExp
        };
        return (player, rank);
    }
}
