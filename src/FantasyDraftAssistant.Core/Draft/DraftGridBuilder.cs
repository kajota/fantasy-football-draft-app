using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Engine;

public sealed record DraftGridTeam(string Label, bool IsMine, TeamId TeamId);

public sealed record DraftGridCell(
    string Player,
    string Position,
    string RoundPick,
    bool IsCurrent,
    bool IsEmpty,
    bool IsMine);

public sealed record DraftGridRound(int Round, IReadOnlyList<DraftGridCell> Cells);

public sealed record DraftGrid(
    IReadOnlyList<DraftGridTeam> Teams,
    IReadOnlyList<DraftGridRound> Rounds);

public sealed record DraftGridPick(
    int OverallPick,
    TeamId TeamId,
    int Round,
    string Player,
    string Position);

public static class DraftGridBuilder
{
    public static DraftGrid Build(
        IReadOnlyList<Team> teams,
        IReadOnlyList<DraftSlot> slots,
        IReadOnlyDictionary<int, DraftGridPick> picksByOverall,
        int? currentOverallPick,
        TeamId? userTeamId)
    {
        var orderedTeams = OrderTeams(teams, slots);
        var headers = orderedTeams
            .Select(team => new DraftGridTeam(
                team.Label,
                userTeamId is { } mine && team.TeamId.Equals(mine),
                team.TeamId))
            .ToList();
        var maxRound = slots.Count == 0 ? 0 : slots.Max(slot => slot.Round);
        var rounds = new List<DraftGridRound>(maxRound);
        for (var round = 1; round <= maxRound; round++)
        {
            var cells = new List<DraftGridCell>(orderedTeams.Count);
            foreach (var team in orderedTeams)
            {
                var slot = slots.FirstOrDefault(s => s.TeamId.Equals(team.TeamId) && s.Round == round);
                if (slot is null)
                {
                    cells.Add(new DraftGridCell("", "", "", false, true, false));
                    continue;
                }

                var isCurrent = currentOverallPick is { } current && slot.OverallPick == current;
                picksByOverall.TryGetValue(slot.OverallPick, out var pick);
                cells.Add(new DraftGridCell(
                    pick?.Player ?? (isCurrent ? "On the clock" : ""),
                    pick?.Position ?? "",
                    $"{slot.Round}.{slot.RoundPick:00}",
                    isCurrent,
                    pick is null,
                    userTeamId is { } mine && team.TeamId.Equals(mine)));
            }

            rounds.Add(new DraftGridRound(round, cells));
        }

        return new DraftGrid(headers, rounds);
    }

    private static List<Team> OrderTeams(IReadOnlyList<Team> teams, IReadOnlyList<DraftSlot> slots)
    {
        var byId = teams.ToDictionary(team => team.TeamId);
        var ordered = new List<Team>();
        foreach (var teamId in slots.Where(slot => slot.Round == 1).OrderBy(slot => slot.RoundPick).Select(slot => slot.TeamId))
        {
            if (byId.TryGetValue(teamId, out var team) && ordered.All(existing => !existing.TeamId.Equals(team.TeamId)))
                ordered.Add(team);
        }

        foreach (var team in teams.OrderBy(t => t.DraftPosition).ThenBy(t => t.Label, StringComparer.OrdinalIgnoreCase))
        {
            if (ordered.All(existing => !existing.TeamId.Equals(team.TeamId)))
                ordered.Add(team);
        }

        return ordered;
    }
}
