using FantasyDraftAssistant.Core.Engine;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Analytics;

public sealed record DraftTeamGrade(
    TeamId TeamId,
    string TeamName,
    bool IsUser,
    string Letter,
    int Score,
    string Headline,
    decimal StarterPoints,
    double AverageValue,
    int OpenStarters,
    IReadOnlyList<string> Notes);

public static class DraftGrader
{
    public static IReadOnlyList<DraftTeamGrade> Grade(
        DraftWorkingState state,
        IReadOnlyList<Player> players,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        IReadOnlyDictionary<PlayerId, PlayerProjection> projections)
    {
        var catalog = players.ToDictionary(player => player.PlayerId);
        var teams = state.Teams
            .OrderBy(team => team.DraftPosition)
            .Select(team => GradeTeam(state, team, catalog, rankings, adp, projections))
            .ToList();

        if (teams.Count == 0)
            return teams;

        var withPoints = teams.Where(team => team.StarterPoints > 0).Select(team => team.StarterPoints).ToList();
        var median = withPoints.Count == 0 ? 0m : Median(withPoints);

        return teams
            .Select(team => Relativize(team, median, teams.Count(item => item.StarterPoints > team.StarterPoints) + 1))
            .OrderByDescending(team => team.Score)
            .ThenBy(team => team.TeamName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string Letter(int score) => score switch
    {
        >= 97 => "A+",
        >= 93 => "A",
        >= 90 => "A-",
        >= 87 => "B+",
        >= 83 => "B",
        >= 80 => "B-",
        >= 77 => "C+",
        >= 73 => "C",
        >= 70 => "C-",
        >= 67 => "D+",
        >= 63 => "D",
        >= 60 => "D-",
        _ => "F"
    };

    private static DraftTeamGrade GradeTeam(
        DraftWorkingState state,
        Team team,
        IReadOnlyDictionary<PlayerId, Player> catalog,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        IReadOnlyDictionary<PlayerId, PlayerProjection> projections)
    {
        var isUser = state.League.UserTeamId is { } user && team.TeamId.Equals(user);
        var picks = state.SelectionsForTeam(team.TeamId)
            .OrderBy(selection => selection.OverallPick)
            .Select(selection =>
            {
                catalog.TryGetValue(selection.PlayerId, out var player);
                rankings.TryGetValue(selection.PlayerId, out var ranking);
                adp.TryGetValue(selection.PlayerId, out var playerAdp);
                projections.TryGetValue(selection.PlayerId, out var projection);
                return new GradedPick(
                    player?.Name ?? selection.PlayerId.ToString(),
                    player?.PrimaryPosition ?? PlayerPosition.WR,
                    player?.NflTeam ?? "",
                    selection.OverallPick,
                    $"{selection.Round}.{selection.RoundPick:00}",
                    ranking?.OverallRank,
                    playerAdp is { OverallAdp: > 0 } ? playerAdp.OverallAdp : null,
                    projection is null ? null : ProjectionScorer.Score(projection, state.ScoringRules));
            })
            .ToList();

        if (picks.Count == 0)
        {
            return new DraftTeamGrade(
                team.TeamId,
                team.Label,
                isUser,
                "—",
                0,
                "No picks to grade.",
                0,
                0,
                RosterRules.StartingSlotCount(state.RosterSlots),
                ["No players drafted."]);
        }

        var board = RosterBoardBuilder.Build(
            state.RosterSlots,
            picks.Select(pick => new RosterBoardPlayer(pick.Name, pick.Position, pick.NflTeam, pick.RoundPick, pick.OverallPick)).ToList());
        var starterKeys = board.Slots
            .Where(slot => slot.IsFilled && slot.Kind is SlotKind.Required or SlotKind.Flex)
            .Select(slot => (slot.Player, slot.RoundPick))
            .ToHashSet();
        var starterPoints = picks
            .Where(pick => starterKeys.Contains((pick.Name, pick.RoundPick)))
            .Sum(pick => pick.ProjectedPoints ?? 0);
        var values = picks.Where(pick => pick.Adp is not null).Select(pick => pick.OverallPick - pick.Adp!.Value).ToList();
        var averageValue = values.Count == 0 ? 0 : values.Average();
        var openStarters = board.OpenNeeds.Count;
        var notes = new List<string>();

        var score = 75d;
        score += Math.Clamp(averageValue, -18, 18) * 0.7;
        score -= openStarters * 7;

        if (values.Count > 0)
        {
            var best = picks
                .Where(pick => pick.Adp is not null)
                .OrderByDescending(pick => pick.OverallPick - pick.Adp!.Value)
                .First();
            var worst = picks
                .Where(pick => pick.Adp is not null)
                .OrderBy(pick => pick.OverallPick - pick.Adp!.Value)
                .First();
            var bestDelta = best.OverallPick - best.Adp!.Value;
            var worstDelta = worst.OverallPick - worst.Adp!.Value;
            if (bestDelta >= 4)
                notes.Add($"Best value: {best.Name} at {best.RoundPick} (ADP {AdpConverter.FormatRoundPick(best.Adp.Value, state.League.TeamCount)}).");
            if (worstDelta <= -6)
                notes.Add($"Biggest reach: {worst.Name} at {worst.RoundPick} (ADP {AdpConverter.FormatRoundPick(worst.Adp.Value, state.League.TeamCount)}).");
            notes.Add(averageValue >= 1.5
                ? $"Averaged {averageValue:0.0} picks of ADP value."
                : averageValue <= -1.5
                    ? $"Averaged {Math.Abs(averageValue):0.0} picks earlier than ADP."
                    : "Picks were close to ADP on average.");
        }
        else
        {
            notes.Add("No ADP in the cache for these picks, so value is ungraded.");
        }

        if (starterPoints > 0)
            notes.Add($"Projected starters: {starterPoints:0} pts (league scoring).");
        if (openStarters > 0)
            notes.Add(board.NeedsLine);
        else
            notes.Add("Starting lineup is filled.");

        var letter = Letter((int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero));
        return new DraftTeamGrade(
            team.TeamId,
            team.Label,
            isUser,
            letter,
            (int)Math.Round(Math.Clamp(score, 0, 100), MidpointRounding.AwayFromZero),
            $"{letter} · {picks.Count} pick(s)",
            starterPoints,
            averageValue,
            openStarters,
            notes);
    }

    private static DraftTeamGrade Relativize(DraftTeamGrade team, decimal medianStarterPoints, int projRank)
    {
        if (team.Score == 0 && team.Letter == "—")
            return team;

        var notes = team.Notes.ToList();
        var score = (double)team.Score;
        if (medianStarterPoints > 0 && team.StarterPoints > 0)
        {
            var gap = (double)(team.StarterPoints - medianStarterPoints);
            var bump = Math.Clamp(gap / Math.Max(20d, (double)medianStarterPoints * 0.08), -12, 12);
            score = Math.Clamp(score + bump, 0, 100);
            notes.Add(gap >= 15
                ? $"Starters rank #{projRank} and sit {gap:0} pts above the room median."
                : gap <= -15
                    ? $"Starters rank #{projRank} and sit {Math.Abs(gap):0} pts below the room median."
                    : $"Starters rank #{projRank} in projected points.");
        }

        var letter = Letter((int)Math.Round(score, MidpointRounding.AwayFromZero));
        var headline = team.OpenStarters > 0
            ? $"{letter} · {team.OpenStarters} starting hole(s)"
            : $"{letter} · starters {team.StarterPoints:0} pts";
        return team with
        {
            Letter = letter,
            Score = (int)Math.Round(score, MidpointRounding.AwayFromZero),
            Headline = headline,
            Notes = notes
        };
    }

    private static decimal Median(IReadOnlyList<decimal> values)
    {
        var ordered = values.OrderBy(value => value).ToList();
        var mid = ordered.Count / 2;
        return ordered.Count % 2 == 1 ? ordered[mid] : (ordered[mid - 1] + ordered[mid]) / 2m;
    }

    private sealed record GradedPick(
        string Name,
        PlayerPosition Position,
        string NflTeam,
        int OverallPick,
        string RoundPick,
        int? OverallRank,
        double? Adp,
        decimal? ProjectedPoints);
}
