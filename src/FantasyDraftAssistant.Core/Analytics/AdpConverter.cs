namespace FantasyDraftAssistant.Core.Analytics;

public static class AdpConverter
{
    public static (int Round, int Pick) ToRoundPick(double overallAdp, int teamCount)
    {
        if (teamCount < 1)
            throw new ArgumentOutOfRangeException(nameof(teamCount));
        if (overallAdp <= 0)
            return (1, 1);

        var overall = (int)Math.Floor(overallAdp);
        if (overall < 1)
            overall = 1;

        var round = ((overall - 1) / teamCount) + 1;
        var pick = ((overall - 1) % teamCount) + 1;
        return (round, pick);
    }

    public static string FormatRoundPick(double overallAdp, int teamCount)
    {
        var (round, pick) = ToRoundPick(overallAdp, teamCount);
        return $"{round}.{pick:00}";
    }

    public static int ToOverall(int round, int pick, int teamCount)
    {
        if (round < 1 || pick < 1 || teamCount < 1)
            throw new ArgumentOutOfRangeException();
        return ((round - 1) * teamCount) + pick;
    }

    public static double ScaleToLeague(double overallAdp, int teamCount, int referenceTeamCount = 12)
    {
        if (overallAdp <= 0 || teamCount < 1 || referenceTeamCount < 1)
            return overallAdp;
        return overallAdp * teamCount / referenceTeamCount;
    }
}
