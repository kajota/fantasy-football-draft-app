using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

public static class MockPickPolicy
{
    public static PlayerId? Choose(
        DraftWorkingState state,
        IReadOnlyList<Player> players,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        MockPersonality personality)
    {
        var slot = state.CurrentSlot;
        if (slot is null)
            return null;

        var available = players
            .Where(player => !state.UnavailablePlayers.Contains(player.PlayerId))
            .ToList();
        if (available.Count == 0)
            return null;

        var drafted = state.SelectionsForTeam(slot.TeamId)
            .Select(selection => players.FirstOrDefault(player => player.PlayerId.Equals(selection.PlayerId))?.PrimaryPosition)
            .Where(position => position.HasValue)
            .Select(position => position!.Value)
            .ToList();
        var needs = RosterRules.RemainingNeeds(state.RosterSlots, drafted);
        var required = RosterRules.RequiredStartingCounts(state.RosterSlots);
        var counts = drafted
            .GroupBy(position => position)
            .ToDictionary(group => group.Key, group => group.Count());
        var qbDemand = RosterRules.QbDemand(state.RosterSlots);
        var qbCount = counts.GetValueOrDefault(PlayerPosition.QB);
        var rbCount = counts.GetValueOrDefault(PlayerPosition.RB);

        return available
            .OrderBy(player => Score(
                player,
                slot,
                state.League.RoundCount,
                personality,
                rankings,
                adp,
                needs,
                required,
                counts,
                qbDemand,
                qbCount,
                rbCount))
            .ThenBy(player => RankOf(player.PlayerId, rankings))
            .ThenBy(player => player.Name, StringComparer.OrdinalIgnoreCase)
            .First().PlayerId;
    }

    internal static double Score(
        Player player,
        DraftSlot slot,
        int roundCount,
        MockPersonality personality,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        IReadOnlyDictionary<PlayerPosition, int> needs,
        IReadOnlyDictionary<PlayerPosition, int> required,
        IReadOnlyDictionary<PlayerPosition, int> counts,
        int qbDemand,
        int qbCount,
        int rbCount)
    {
        var rank = RankOf(player.PlayerId, rankings);
        var adpValue = adp.TryGetValue(player.PlayerId, out var adpRow) ? adpRow.OverallAdp : rank;
        var score = personality == MockPersonality.AdpHunter ? adpValue : rank;
        var position = player.PrimaryPosition;
        var need = needs.GetValueOrDefault(position);
        var destNeeded = (needs.GetValueOrDefault(PlayerPosition.K) > 0 ? 1 : 0)
                         + (needs.GetValueOrDefault(PlayerPosition.DEF) > 0 ? 1 : 0);
        var picksLeft = Math.Max(1, roundCount - slot.Round + 1);
        var mustFillDest = destNeeded > 0 && picksLeft <= destNeeded + 1;
        var lateRound = Math.Max(1, roundCount - 1);

        if (need > 0)
            score -= 6;
        else if (position is PlayerPosition.RB or PlayerPosition.WR
                 && counts.GetValueOrDefault(position) >= required.GetValueOrDefault(position) + 2)
        {
            score += 12;
        }

        if (position is PlayerPosition.K or PlayerPosition.DEF)
        {
            if (need <= 0)
                score += 40;
            else if (mustFillDest || slot.Round >= lateRound)
                score -= 80;
            else
                score += 50;
        }
        else if (mustFillDest)
        {
            score += 40;
        }

        switch (personality)
        {
            case MockPersonality.ZeroRb:
                if (position == PlayerPosition.RB && slot.Round <= 8)
                    score += 35;
                if (position == PlayerPosition.WR && slot.Round <= 5)
                    score -= 8;
                break;
            case MockPersonality.RbFirst:
                if (position == PlayerPosition.RB && slot.Round <= 4)
                    score -= 22;
                if (position == PlayerPosition.WR && slot.Round <= 2)
                    score += 6;
                break;
            case MockPersonality.HeroRb:
                if (position == PlayerPosition.RB && rbCount == 0 && slot.Round <= 5)
                    score -= 28;
                if (position == PlayerPosition.RB && rbCount >= 1 && slot.Round <= 6)
                    score += 14;
                break;
            case MockPersonality.WrHeavy:
                if (position == PlayerPosition.WR && slot.Round <= 6)
                    score -= 16;
                if (position == PlayerPosition.RB && slot.Round <= 3)
                    score += 8;
                break;
            case MockPersonality.QbEarly:
                if (position == PlayerPosition.QB && qbCount == 0 && slot.Round <= 5)
                    score -= qbDemand >= 2 ? 20 : 6;
                if (position == PlayerPosition.QB && qbCount >= Math.Max(1, qbDemand))
                    score += 25;
                break;
            case MockPersonality.LateQb:
                if (position == PlayerPosition.QB && qbCount == 0 && slot.Round <= Math.Min(10, Math.Max(1, roundCount - 3)))
                    score += 18;
                break;
            case MockPersonality.RookieHunter:
                if (player.YearsExp == 0)
                    score -= 14;
                break;
        }

        var tie = (player.PlayerId.Value.GetHashCode() ^ slot.TeamId.Value.GetHashCode()) & 7;
        return score + tie * 0.01;
    }

    private static double RankOf(PlayerId playerId, IReadOnlyDictionary<PlayerId, PlayerRanking> rankings) =>
        rankings.TryGetValue(playerId, out var ranking) ? ranking.OverallRank : 400;
}
