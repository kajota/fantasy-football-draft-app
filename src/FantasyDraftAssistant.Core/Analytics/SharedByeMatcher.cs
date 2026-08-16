using FantasyDraftAssistant.Core.Enums;

namespace FantasyDraftAssistant.Core.Analytics;

public sealed record RosterByePlayer(string Name, PlayerPosition Position, int? ByeWeek);

public sealed record SharedByeMatch(int ByeWeek, IReadOnlyList<string> TeammateNames, PlayerPosition Position);

public static class SharedByeMatcher
{
    public static SharedByeMatch? For(RosterByePlayer available, IReadOnlyList<RosterByePlayer> roster)
    {
        if (available.ByeWeek is not { } bye || bye < 1)
            return null;

        var names = new List<string>();
        foreach (var mine in roster)
        {
            if (mine.Position != available.Position)
                continue;
            if (mine.ByeWeek != bye)
                continue;
            if (mine.Name.Equals(available.Name, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!names.Contains(mine.Name, StringComparer.OrdinalIgnoreCase))
                names.Add(mine.Name);
        }

        return names.Count == 0 ? null : new SharedByeMatch(bye, names, available.Position);
    }

    public static string Teammates(SharedByeMatch match) => string.Join(", ", match.TeammateNames);

    public static string Label(SharedByeMatch match) => $"Bye {match.ByeWeek} · {Teammates(match)}";

    public static string Detail(SharedByeMatch match) =>
        $"Same {match.Position} bye week ({match.ByeWeek}) as {Teammates(match)} on your roster. Those {match.Position}s would both be idle that week.";
}
