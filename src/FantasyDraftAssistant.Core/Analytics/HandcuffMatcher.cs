using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Analytics;

public sealed record HandcuffPlayer(
    string Name,
    string NflTeam,
    PlayerPosition Position,
    int? OverallRank,
    double? OverallAdp);

public sealed record HandcuffMatch(string StarterName, string NflTeam, PlayerPosition Position);

public static class HandcuffMatcher
{
    public static bool IsHandcuffPosition(PlayerPosition position) =>
        position is PlayerPosition.RB or PlayerPosition.QB;

    public static HandcuffMatch? For(HandcuffPlayer available, IReadOnlyList<HandcuffPlayer> roster)
    {
        if (!IsHandcuffPosition(available.Position))
            return null;
        if (!HasTeam(available.NflTeam))
            return null;

        HandcuffPlayer? starter = null;
        foreach (var mine in roster)
        {
            if (!IsHandcuffPosition(mine.Position))
                continue;
            if (mine.Position != available.Position)
                continue;
            if (!mine.NflTeam.Equals(available.NflTeam, StringComparison.OrdinalIgnoreCase))
                continue;
            if (mine.Name.Equals(available.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!IsAhead(mine, available))
                continue;
            if (starter is null || IsAhead(mine, starter))
                starter = mine;
        }

        return starter is null
            ? null
            : new HandcuffMatch(starter.Name, available.NflTeam.Trim().ToUpperInvariant(), available.Position);
    }

    public static string Label(HandcuffMatch match) => $"Cuff · {match.StarterName}";

    public static string Detail(HandcuffMatch match) =>
        $"Same {match.NflTeam} {match.Position} room as {match.StarterName} on your roster.";

    private static bool HasTeam(string team)
    {
        if (string.IsNullOrWhiteSpace(team))
            return false;
        var token = team.Trim().ToUpperInvariant();
        return token is not "FA" and not "UNK" and not "?" and not "—";
    }

    internal static bool IsAhead(HandcuffPlayer left, HandcuffPlayer right)
    {
        if (left.OverallRank is { } leftRank && right.OverallRank is { } rightRank)
            return leftRank < rightRank;
        if (left.OverallAdp is { } leftAdp && right.OverallAdp is { } rightAdp)
            return leftAdp < rightAdp;
        if (left.OverallRank is not null && right.OverallRank is null)
            return true;
        if (left.OverallAdp is not null && right.OverallAdp is null)
            return true;
        return false;
    }
}
