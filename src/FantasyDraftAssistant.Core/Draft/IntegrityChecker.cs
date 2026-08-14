using FantasyDraftAssistant.Core.Query;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

public static class IntegrityChecker
{
    public static IntegrityReport Validate(DraftWorkingState state)
    {
        var issues = new List<string>();

        var players = state.ActiveSelections.Values.GroupBy(s => s.PlayerId).Where(g => g.Count() > 1);
        foreach (var group in players)
            issues.Add($"Player {group.Key} is active on {group.Count()} selections.");

        var slots = state.ActiveSelections.Values.GroupBy(s => s.OverallPick).Where(g => g.Count() > 1);
        foreach (var group in slots)
            issues.Add($"Overall pick {group.Key} has {group.Count()} active selections.");

        foreach (var selection in state.ActiveSelections.Values)
        {
            var slot = state.Slots.FirstOrDefault(s => s.OverallPick == selection.OverallPick);
            if (slot is null)
            {
                issues.Add($"Active selection at pick {selection.OverallPick} has no matching slot.");
                continue;
            }

            if (!slot.TeamId.Equals(selection.TeamId))
                issues.Add($"Pick {selection.OverallPick} is assigned to a team that does not own the slot.");
        }

        foreach (var playerId in state.UnavailablePlayers)
        {
            var drafted = state.ActiveSelections.Values.Any(s => s.PlayerId.Equals(playerId));
            var keeper = state.Keepers.Any(k => k.PlayerId.Equals(playerId));
            if (!drafted && !keeper)
                issues.Add($"Player {playerId} is unavailable without an active selection or keeper assignment.");
        }

        foreach (var selection in state.ActiveSelections.Values)
        {
            if (!state.UnavailablePlayers.Contains(selection.PlayerId))
                issues.Add($"Player {selection.PlayerId} is active but still marked available.");
        }

        var expectedCurrent = state.Slots
            .OrderBy(s => s.OverallPick)
            .FirstOrDefault(s => !state.ActiveSelections.ContainsKey(s.OverallPick));
        if (expectedCurrent is not null && state.CurrentSlot?.OverallPick != expectedCurrent.OverallPick)
            issues.Add("Current pick does not follow the active timeline.");

        var extraKeepers = state.Keepers.GroupBy(k => k.TeamId).Where(g => g.Count() > 1);
        foreach (var group in extraKeepers)
            issues.Add($"Team {group.Key} has {group.Count()} keepers.");

        return new IntegrityReport
        {
            IsHealthy = issues.Count == 0,
            Issues = issues
        };
    }
}
