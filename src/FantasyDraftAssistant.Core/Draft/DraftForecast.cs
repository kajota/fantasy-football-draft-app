using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

/// <summary>One seat's likely next pick, as guessed by the practice-draft policy.</summary>
public sealed record PredictedPick(
    TeamId TeamId,
    int OverallPick,
    string RoundPick,
    PlayerId PlayerId,
    string PlayerName,
    string Position);

/// <summary>
/// Guesses what the seats between now and the user's next pick will take.
///
/// This runs the same policy CPU seats use in practice drafts: rankings, roster needs, and the
/// seat's personality. Against real managers it is a heuristic, not a forecast — it is reliable
/// about which positions are under pressure, and routinely wrong about individual names.
/// </summary>
public static class DraftForecast
{
    /// <summary>
    /// Simulates forward to the user's next pick.
    ///
    /// The passed state is mutated as picks are simulated onto it, so callers must hand over a
    /// freshly loaded state they will then throw away — never one that will be persisted.
    /// </summary>
    public static IReadOnlyList<PredictedPick> SimulateOnto(
        DraftWorkingState scratch,
        IReadOnlyList<Player> players,
        IReadOnlyDictionary<PlayerId, PlayerRanking> rankings,
        IReadOnlyDictionary<PlayerId, PlayerAdp> adp,
        Func<TeamId, MockPersonality> personalityFor,
        TeamId? userTeamId,
        int maxPicks = 32)
    {
        var byId = players.ToDictionary(player => player.PlayerId);
        var predictions = new List<PredictedPick>();

        for (var i = 0; i < maxPicks; i++)
        {
            var slot = scratch.CurrentSlot;
            if (slot is null)
                break;
            // Stop at the user's own pick: everything after it depends on what they take.
            if (userTeamId is { } user && slot.TeamId.Equals(user))
                break;

            var chosen = MockPickPolicy.Choose(scratch, players, rankings, adp, personalityFor(slot.TeamId));
            if (chosen is not { } playerId || !byId.TryGetValue(playerId, out var player))
                break;

            predictions.Add(new PredictedPick(
                slot.TeamId,
                slot.OverallPick,
                $"{slot.Round}.{slot.RoundPick:00}",
                playerId,
                player.Name,
                player.PrimaryPosition.ToString()));

            Apply(scratch, slot, playerId);
        }

        return predictions;
    }

    /// <summary>Records a simulated pick so the next iteration sees an advanced board.</summary>
    private static void Apply(DraftWorkingState scratch, DraftSlot slot, PlayerId playerId)
    {
        scratch.ActiveSelections[slot.OverallPick] = new ActiveSelection
        {
            EventId = EventId.New(),
            DraftId = scratch.Draft.DraftId,
            BranchId = scratch.ActiveBranch.BranchId,
            DraftSlotId = slot.DraftSlotId,
            OverallPick = slot.OverallPick,
            Round = slot.Round,
            RoundPick = slot.RoundPick,
            TeamId = slot.TeamId,
            PlayerId = playerId,
            Source = PickSource.Manual,
            ObservedAt = DateTimeOffset.UtcNow
        };
        scratch.UnavailablePlayers.Add(playerId);
    }
}
