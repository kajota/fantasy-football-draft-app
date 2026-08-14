using FantasyDraftAssistant.Core.Commands;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

public static class KeeperRules
{
    public const int MaxKeepersPerTeam = 1;

    public static ValidationResult Validate(IReadOnlyList<KeeperSpec> keepers)
    {
        var byTeam = keepers.GroupBy(k => k.TeamId).Where(g => g.Count() > MaxKeepersPerTeam).ToList();
        if (byTeam.Count > 0)
        {
            return ValidationResult.Fail(
                $"Team {byTeam[0].Key} has {byTeam[0].Count()} keepers; the limit is {MaxKeepersPerTeam} per team.");
        }

        var byPlayer = keepers.GroupBy(k => k.PlayerId).Where(g => g.Count() > 1).ToList();
        if (byPlayer.Count > 0)
            return ValidationResult.Fail("A player cannot be kept by more than one team.");

        foreach (var keeper in keepers)
        {
            if (keeper.RoundCost < 1)
                return ValidationResult.Fail("Keeper round cost must be at least 1.");
        }

        return ValidationResult.Ok();
    }

    public static DraftSlot? ResolveSlot(IReadOnlyList<DraftSlot> slots, Keeper keeper)
    {
        if (keeper.DraftSlotId is { } assigned)
            return slots.FirstOrDefault(s => s.DraftSlotId.Equals(assigned));

        return slots
            .Where(s => s.TeamId.Equals(keeper.TeamId) && s.Round == keeper.RoundCost)
            .OrderBy(s => s.OverallPick)
            .FirstOrDefault();
    }

    public static IReadOnlyList<string> FlagImportedOverLimit(IReadOnlyList<Keeper> imported)
    {
        return imported
            .GroupBy(k => k.TeamId)
            .Where(g => g.Count() > MaxKeepersPerTeam)
            .Select(g => $"Team {g.Key} imported {g.Count()} keepers; only one is allowed.")
            .ToList();
    }
}
