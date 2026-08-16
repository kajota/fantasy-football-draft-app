using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Analytics;

public sealed class AnalyticsSnapshot
{
    public required int CurrentOverallPick { get; init; }
    public required string CurrentRoundPick { get; init; }
    public TeamId? CurrentTeamId { get; init; }
    public int? UserNextOverallPick { get; init; }
    public string? UserNextRoundPick { get; init; }
    public int PicksUntilUser { get; init; }
    public required IReadOnlyDictionary<PlayerPosition, int> DraftedByPosition { get; init; }
    public required IReadOnlyDictionary<PlayerPosition, int> AvailableByPosition { get; init; }
    public required IReadOnlyList<PlayerPosition> RecentPositions { get; init; }
    public required IReadOnlyDictionary<int, int> RemainingByTier { get; init; }
    public required IReadOnlyList<TeamNeedSummary> TeamNeeds { get; init; }
    public required IReadOnlyList<DraftAlert> Alerts { get; init; }
    public required int QbDemand { get; init; }
    public required bool ElevatedQbDemand { get; init; }
}

public sealed class TeamNeedSummary
{
    public required TeamId TeamId { get; init; }
    public required string TeamName { get; init; }
    public required IReadOnlyDictionary<PlayerPosition, int> RemainingNeeds { get; init; }
}

public sealed class PlayerValuation
{
    public required PlayerId PlayerId { get; init; }
    public int? OverallRank { get; init; }
    public int? PositionRank { get; init; }
    public int? Tier { get; init; }
    public int? RankMin { get; init; }
    public int? RankMax { get; init; }
    public double? RankStd { get; init; }
    public string? RankRange { get; init; }
    public double? OverallAdp { get; init; }
    public string? AdpRoundPick { get; init; }
    public decimal? ProjectedPoints { get; init; }
}

public static class AnalyticsEngine
{
    public static AnalyticsSnapshot Compute(
        DraftWorkingState state,
        IReadOnlyList<Player> players,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        int recentWindow = 7)
    {
        var current = state.CurrentSlot;
        var userTeamId = state.League.UserTeamId;
        var upcoming = state.Slots
            .Where(s => !state.ActiveSelections.ContainsKey(s.OverallPick))
            .OrderBy(s => s.OverallPick)
            .ToList();

        var userNext = userTeamId is { } user
            ? upcoming.FirstOrDefault(s => s.TeamId.Equals(user))
            : null;

        var draftedByPosition = Enum.GetValues<PlayerPosition>().ToDictionary(p => p, _ => 0);
        var availableByPosition = Enum.GetValues<PlayerPosition>().ToDictionary(p => p, _ => 0);
        var playerLookup = players.ToDictionary(p => p.PlayerId);

        foreach (var selection in state.ActiveSelections.Values)
        {
            if (playerLookup.TryGetValue(selection.PlayerId, out var drafted))
                draftedByPosition[drafted.PrimaryPosition]++;
        }

        foreach (var player in players)
        {
            if (!state.UnavailablePlayers.Contains(player.PlayerId))
                availableByPosition[player.PrimaryPosition]++;
        }

        var recent = state.ActiveSelections.Values
            .OrderByDescending(s => s.OverallPick)
            .Take(recentWindow)
            .Select(s => playerLookup.TryGetValue(s.PlayerId, out var p) ? p.PrimaryPosition : (PlayerPosition?)null)
            .Where(p => p.HasValue)
            .Select(p => p!.Value)
            .ToList();

        var remainingByTier = new Dictionary<int, int>();
        foreach (var player in players.Where(p => !state.UnavailablePlayers.Contains(p.PlayerId)))
        {
            if (!rankings.TryGetValue(player.PlayerId, out var ranking) || ranking.Tier is null)
                continue;
            remainingByTier[ranking.Tier.Value] = remainingByTier.GetValueOrDefault(ranking.Tier.Value) + 1;
        }

        var teamNeeds = state.Teams
            .OrderBy(t => t.DraftPosition)
            .Select(team =>
            {
                var positions = state.SelectionsForTeam(team.TeamId)
                    .Select(s => playerLookup.TryGetValue(s.PlayerId, out var p) ? p.PrimaryPosition : (PlayerPosition?)null)
                    .Where(p => p.HasValue)
                    .Select(p => p!.Value)
                    .ToList();
                return new TeamNeedSummary
                {
                    TeamId = team.TeamId,
                    TeamName = team.Label,
                    RemainingNeeds = RosterRules.RemainingNeeds(state.RosterSlots, positions)
                };
            })
            .ToList();

        var qbDemand = RosterRules.QbDemand(state.RosterSlots);
        var alerts = BuildAlerts(
            state,
            playerLookup,
            rankings,
            adp,
            draftedByPosition,
            availableByPosition,
            remainingByTier,
            recent,
            teamNeeds,
            userNext,
            qbDemand);

        return new AnalyticsSnapshot
        {
            CurrentOverallPick = current?.OverallPick ?? state.Slots.Count + 1,
            CurrentRoundPick = current is null
                ? "Done"
                : DraftSlotGenerator.FormatRoundPick(current.Round, current.RoundPick),
            CurrentTeamId = current?.TeamId,
            UserNextOverallPick = userNext?.OverallPick,
            UserNextRoundPick = userNext is null
                ? null
                : DraftSlotGenerator.FormatRoundPick(userNext.Round, userNext.RoundPick),
            PicksUntilUser = userNext is null || current is null
                ? 0
                : userNext.OverallPick - current.OverallPick,
            DraftedByPosition = draftedByPosition,
            AvailableByPosition = availableByPosition,
            RecentPositions = recent,
            RemainingByTier = remainingByTier,
            TeamNeeds = teamNeeds,
            Alerts = alerts,
            QbDemand = qbDemand,
            ElevatedQbDemand = RosterRules.IsSuperflexOrMultiQb(state.RosterSlots)
        };
    }

    public static PlayerValuation ValuePlayer(
        Player player,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        IReadOnlyDictionary<PlayerId, PlayerProjection> projections,
        IReadOnlyList<ScoringRule> scoring,
        int teamCount)
    {
        rankings.TryGetValue(player.PlayerId, out var ranking);
        adp.TryGetValue(player.PlayerId, out var playerAdp);
        projections.TryGetValue(player.PlayerId, out var projection);

        return new PlayerValuation
        {
            PlayerId = player.PlayerId,
            OverallRank = ranking?.OverallRank,
            PositionRank = ranking?.PositionRank,
            Tier = ranking?.Tier,
            RankMin = ranking?.RankMin,
            RankMax = ranking?.RankMax,
            RankStd = ranking?.RankStd,
            RankRange = FormatRankRange(ranking?.RankMin, ranking?.RankMax),
            OverallAdp = playerAdp?.OverallAdp,
            AdpRoundPick = playerAdp is null ? null : AdpConverter.FormatRoundPick(playerAdp.OverallAdp, teamCount),
            ProjectedPoints = projection is null ? null : ProjectionScorer.Score(projection, scoring)
        };
    }

    public static string? FormatRankRange(int? min, int? max)
    {
        if (min is { } low && max is { } high)
            return $"{low}-{high}";
        if (min is { } onlyMin)
            return $"{onlyMin}-";
        if (max is { } onlyMax)
            return $"-{onlyMax}";
        return null;
    }

    private static List<DraftAlert> BuildAlerts(
        DraftWorkingState state,
        IReadOnlyDictionary<PlayerId, Player> players,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        IReadOnlyDictionary<PlayerPosition, int> draftedByPosition,
        IReadOnlyDictionary<PlayerPosition, int> availableByPosition,
        IReadOnlyDictionary<int, int> remainingByTier,
        IReadOnlyList<PlayerPosition> recent,
        IReadOnlyList<TeamNeedSummary> teamNeeds,
        DraftSlot? userNext,
        int qbDemand)
    {
        var alerts = new List<DraftAlert>();
        var currentPick = state.CurrentSlot?.OverallPick ?? int.MaxValue;

        foreach (var player in players.Values.Where(p => !state.UnavailablePlayers.Contains(p.PlayerId)))
        {
            if (!rankings.TryGetValue(player.PlayerId, out var ranking))
                continue;
            if (ranking.OverallRank + 12 < currentPick)
            {
                alerts.Add(new DraftAlert
                {
                    Kind = AlertKind.Value,
                    Severity = 2,
                    Message = $"Fantasy ranking #{ranking.OverallRank} {player.Name} remains available at pick {currentPick}."
                });
                break;
            }
        }

        foreach (var (tier, count) in remainingByTier.OrderBy(kv => kv.Key))
        {
            if (count == 1 && tier <= 3)
            {
                alerts.Add(new DraftAlert
                {
                    Kind = AlertKind.Position,
                    Severity = 2,
                    Message = $"Only one Tier {tier} player remains available."
                });
                break;
            }
        }

        if (recent.Count >= 5)
        {
            var top = recent.GroupBy(p => p).OrderByDescending(g => g.Count()).First();
            if (top.Count() >= 4)
            {
                alerts.Add(new DraftAlert
                {
                    Kind = AlertKind.DraftRun,
                    Severity = 2,
                    Message = $"{top.Count()} {top.Key}s have been selected in the previous {recent.Count} picks."
                });
            }
        }

        if (userNext is not null && state.CurrentSlot is not null)
        {
            var until = userNext.OverallPick - state.CurrentSlot.OverallPick;
            if (until is 1 or 2)
            {
                alerts.Add(new DraftAlert
                {
                    Kind = AlertKind.PickApproaching,
                    Severity = 3,
                    Message = until == 1 ? "Your pick is next." : "Your pick is 2 picks away."
                });
            }

            var intervening = state.Slots
                .Where(s => s.OverallPick >= state.CurrentSlot.OverallPick && s.OverallPick < userNext.OverallPick)
                .Select(s => s.TeamId)
                .Distinct()
                .ToList();

            foreach (var position in new[] { PlayerPosition.TE, PlayerPosition.QB, PlayerPosition.RB, PlayerPosition.WR })
            {
                var needing = teamNeeds.Count(t =>
                    intervening.Contains(t.TeamId) && t.RemainingNeeds.GetValueOrDefault(position) > 0);
                if (needing >= 3)
                {
                    alerts.Add(new DraftAlert
                    {
                        Kind = AlertKind.OpponentNeed,
                        Severity = 1,
                        Message = $"{needing} of the teams selecting before your next pick still need a starting {position}."
                    });
                }
            }
        }

        if (qbDemand >= 2)
        {
            var startableQbs = availableByPosition.GetValueOrDefault(PlayerPosition.QB);
            if (startableQbs <= 4 && userNext is not null && state.CurrentSlot is not null)
            {
                var intervening = state.Slots
                    .Where(s => s.OverallPick >= state.CurrentSlot.OverallPick && s.OverallPick < userNext.OverallPick)
                    .Select(s => s.TeamId)
                    .Distinct();
                var needingQb = teamNeeds.Count(t =>
                    intervening.Contains(t.TeamId) && t.RemainingNeeds.GetValueOrDefault(PlayerPosition.QB) > 0);
                if (needingQb > 0)
                {
                    alerts.Add(new DraftAlert
                    {
                        Kind = AlertKind.QbScarcity,
                        Severity = 3,
                        Message = $"Only {startableQbs} QBs remain, and {needingQb} team(s) ahead of your next pick still need a QB."
                    });
                }
            }
        }

        if (state.League.UserTeamId is { } user)
        {
            var roster = state.SelectionsForTeam(user)
                .Select(selection => players.TryGetValue(selection.PlayerId, out var owned)
                    ? ToHandcuff(owned, rankings, adp)
                    : null)
                .Where(owned => owned is not null)
                .Select(owned => owned!)
                .ToList();
            foreach (var cuff in players.Values
                         .Where(player => !state.UnavailablePlayers.Contains(player.PlayerId))
                         .Select(player => (Player: player, Match: HandcuffMatcher.For(ToHandcuff(player, rankings, adp), roster)))
                         .Where(item => item.Match is not null)
                         .OrderBy(item => rankings.TryGetValue(item.Player.PlayerId, out var ranking) ? ranking.OverallRank : 999)
                         .Take(2))
            {
                alerts.Add(new DraftAlert
                {
                    Kind = AlertKind.Handcuff,
                    Severity = 2,
                    Message = $"{cuff.Player.Name} is still available as a {cuff.Match!.StarterName} handcuff."
                });
            }
        }

        _ = draftedByPosition;
        return alerts.OrderByDescending(a => a.Severity).Take(5).ToList();
    }

    private static HandcuffPlayer ToHandcuff(
        Player player,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp)
    {
        rankings.TryGetValue(player.PlayerId, out var ranking);
        adp.TryGetValue(player.PlayerId, out var playerAdp);
        return new HandcuffPlayer(
            player.Name,
            player.NflTeam,
            player.PrimaryPosition,
            ranking?.OverallRank,
            playerAdp?.OverallAdp);
    }
}
