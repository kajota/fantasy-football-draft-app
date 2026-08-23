using FantasyDraftAssistant.Core.Analytics;
using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Core.Engine;

public static class BoardSnapshotBuilder
{
    public static BoardSnapshot Build(
        DraftWorkingState state,
        IReadOnlyList<Player> players,
        DateTimeOffset now,
        IReadOnlyDictionary<PlayerId, PlayerAdp>? adp = null)
    {
        var byId = players.ToDictionary(player => player.PlayerId);
        var teamsById = state.Teams.ToDictionary(team => team.TeamId);
        var gridPicks = new Dictionary<int, DraftGridPick>();
        foreach (var selection in state.ActiveSelections.Values)
        {
            byId.TryGetValue(selection.PlayerId, out var player);
            PlayerAdp? playerAdp = null;
            adp?.TryGetValue(selection.PlayerId, out playerAdp);
            gridPicks[selection.OverallPick] = new DraftGridPick(
                selection.OverallPick,
                selection.TeamId,
                selection.Round,
                player?.Name ?? selection.PlayerId.ToString(),
                player?.PrimaryPosition.ToString() ?? "",
                playerAdp?.OverallAdp,
                selection.Source == PickSource.Keeper);
        }

        var grid = DraftGridBuilder.Build(
            state.Teams,
            state.Slots,
            gridPicks,
            state.CurrentSlot?.OverallPick,
            state.League.UserTeamId);

        var current = state.CurrentSlot;
        BoardClockTeam? onTheClock = null;
        if (current is not null && teamsById.TryGetValue(current.TeamId, out var clockTeam))
        {
            onTheClock = new BoardClockTeam
            {
                Team = clockTeam.Label,
                IsMine = state.League.UserTeamId is { } mine && current.TeamId.Equals(mine)
            };
        }

        BoardLastPick? lastPick = null;
        var last = state.ActiveSelections.Values.OrderByDescending(s => s.OverallPick).FirstOrDefault();
        if (last is not null)
        {
            byId.TryGetValue(last.PlayerId, out var player);
            teamsById.TryGetValue(last.TeamId, out var team);
            lastPick = new BoardLastPick
            {
                Player = player?.Name ?? last.PlayerId.ToString(),
                Position = player?.PrimaryPosition.ToString() ?? "",
                Team = team?.Label ?? "",
                RoundPick = DraftSlotGenerator.FormatRoundPick(last.Round, last.RoundPick)
            };
        }

        return new BoardSnapshot
        {
            UpdatedAt = now,
            League = state.League.Name,
            Practice = state.ActiveBranch.ParentBranchId is not null,
            Round = current?.Round,
            Pick = current is null ? null : DraftSlotGenerator.FormatRoundPick(current.Round, current.RoundPick),
            Overall = current?.OverallPick,
            OnTheClock = onTheClock,
            LastPick = lastPick,
            Teams = grid.Teams.Select(team => new BoardSnapshotTeam
            {
                Label = team.Label,
                IsMine = team.IsMine
            }).ToList(),
            Rounds = grid.Rounds.Select(round => new BoardSnapshotRound
            {
                Round = round.Round,
                Cells = round.Cells.Select(cell => new BoardSnapshotCell
                {
                    Player = cell.IsCurrent && cell.IsEmpty ? "" : cell.Player,
                    Position = cell.Position,
                    RoundPick = cell.RoundPick,
                    IsCurrent = cell.IsCurrent,
                    IsEmpty = cell.IsEmpty,
                    IsMine = cell.IsMine,
                    Heat = PickValueHeat.Css(cell.Heat)
                }).ToList()
            }).ToList()
        };
    }
}
