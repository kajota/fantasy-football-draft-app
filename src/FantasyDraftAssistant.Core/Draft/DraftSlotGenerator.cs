using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Engine;

public sealed record GeneratedSlot(int OverallPick, int Round, int RoundPick, TeamId TeamId);

public static class DraftSlotGenerator
{
    public static IReadOnlyList<GeneratedSlot> Generate(
        DraftType type,
        IReadOnlyList<TeamDraftPosition> teams,
        int roundCount)
    {
        if (teams.Count == 0)
            throw new ArgumentException("At least one team is required.", nameof(teams));
        if (roundCount < 1)
            throw new ArgumentOutOfRangeException(nameof(roundCount), "Round count must be at least 1.");

        var ordered = teams.OrderBy(t => t.DraftPosition).ToList();
        ValidatePositions(ordered);

        return type switch
        {
            DraftType.Snake => GenerateSnake(ordered, roundCount),
            DraftType.Linear => GenerateLinear(ordered, roundCount),
            DraftType.Custom => GenerateLinear(ordered, roundCount),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported draft type.")
        };
    }

    public static IReadOnlyList<DraftSlot> ToDraftSlots(DraftId draftId, IReadOnlyList<GeneratedSlot> generated)
    {
        return generated.Select(slot => new DraftSlot
        {
            DraftSlotId = DraftSlotId.New(),
            DraftId = draftId,
            OverallPick = slot.OverallPick,
            Round = slot.Round,
            RoundPick = slot.RoundPick,
            TeamId = slot.TeamId,
            IsKeeperSlot = false
        }).ToList();
    }

    public static string FormatRoundPick(int round, int roundPick) => $"{round}.{roundPick:00}";

    public static (int Round, int RoundPick) FromOverall(int overallPick, int teamCount)
    {
        if (overallPick < 1)
            throw new ArgumentOutOfRangeException(nameof(overallPick));
        if (teamCount < 1)
            throw new ArgumentOutOfRangeException(nameof(teamCount));

        var round = ((overallPick - 1) / teamCount) + 1;
        var roundPick = ((overallPick - 1) % teamCount) + 1;
        return (round, roundPick);
    }

    private static List<GeneratedSlot> GenerateLinear(IReadOnlyList<TeamDraftPosition> ordered, int roundCount)
    {
        var slots = new List<GeneratedSlot>(ordered.Count * roundCount);
        var overall = 1;
        for (var round = 1; round <= roundCount; round++)
        {
            for (var i = 0; i < ordered.Count; i++)
            {
                slots.Add(new GeneratedSlot(overall, round, i + 1, RequireTeamId(ordered[i])));
                overall++;
            }
        }

        return slots;
    }

    private static List<GeneratedSlot> GenerateSnake(IReadOnlyList<TeamDraftPosition> ordered, int roundCount)
    {
        var slots = new List<GeneratedSlot>(ordered.Count * roundCount);
        var overall = 1;
        for (var round = 1; round <= roundCount; round++)
        {
            var reverse = round % 2 == 0;
            for (var i = 0; i < ordered.Count; i++)
            {
                var index = reverse ? ordered.Count - 1 - i : i;
                slots.Add(new GeneratedSlot(overall, round, i + 1, RequireTeamId(ordered[index])));
                overall++;
            }
        }

        return slots;
    }

    private static TeamId RequireTeamId(TeamDraftPosition team)
    {
        if (team.TeamId is not { } id)
            throw new InvalidOperationException($"Team '{team.Name}' is missing a TeamId.");
        return id;
    }

    private static void ValidatePositions(IReadOnlyList<TeamDraftPosition> ordered)
    {
        for (var i = 0; i < ordered.Count; i++)
        {
            if (ordered[i].DraftPosition != i + 1)
            {
                throw new ArgumentException(
                    "Draft positions must be a contiguous sequence starting at 1.",
                    nameof(ordered));
            }
        }
    }
}
