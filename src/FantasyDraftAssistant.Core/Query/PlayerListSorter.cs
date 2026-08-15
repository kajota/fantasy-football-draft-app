namespace FantasyDraftAssistant.Core.Query;

public static class PlayerListSorter
{
    public static IReadOnlyList<PlayerSummaryDto> Sort(
        IEnumerable<PlayerSummaryDto> players,
        PlayerListSort sortBy,
        bool descending,
        int? maxResults = null)
    {
        IOrderedEnumerable<PlayerSummaryDto> ordered = sortBy switch
        {
            PlayerListSort.Name => descending
                ? players.OrderByDescending(p => p.Name)
                : players.OrderBy(p => p.Name),
            PlayerListSort.Position => descending
                ? players.OrderByDescending(p => p.Position)
                : players.OrderBy(p => p.Position),
            PlayerListSort.NflTeam => descending
                ? players.OrderByDescending(p => p.NflTeam)
                : players.OrderBy(p => p.NflTeam),
            PlayerListSort.Adp => descending
                ? players.OrderByDescending(p => p.OverallAdp ?? double.MinValue)
                : players.OrderBy(p => p.OverallAdp ?? double.MaxValue),
            PlayerListSort.ProjectedPoints => descending
                ? players.OrderByDescending(p => p.ProjectedPoints ?? decimal.MinValue)
                : players.OrderBy(p => p.ProjectedPoints ?? decimal.MaxValue),
            _ => descending
                ? players.OrderByDescending(p => p.OverallRank ?? int.MinValue)
                : players.OrderBy(p => p.OverallRank ?? int.MaxValue)
        };

        var result = ordered.ThenBy(p => p.Name);
        return maxResults is { } take
            ? result.Take(take).ToList()
            : result.ToList();
    }
}
