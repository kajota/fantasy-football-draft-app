namespace FantasyDraftAssistant.Core.Analytics;

public enum PickHeat
{
    Empty = 0,
    Unknown = 1,
    Keeper = 2,
    Steal = 3,
    MildSteal = 4,
    Fair = 5,
    MildReach = 6,
    Reach = 7
}

public static class PickValueHeat
{
    public static PickHeat From(int overallPick, double? overallAdp, int teamCount, bool isKeeper = false)
    {
        if (overallPick < 1)
            return PickHeat.Empty;
        if (isKeeper)
            return PickHeat.Keeper;
        if (overallAdp is null or <= 0)
            return PickHeat.Unknown;

        var teams = Math.Max(teamCount, 1);
        var adp = AdpConverter.ScaleToLeague(overallAdp.Value, teams);
        var delta = overallPick - adp;
        var round = teams;
        if (delta >= round)
            return PickHeat.Steal;
        if (delta >= round / 3.0)
            return PickHeat.MildSteal;
        if (delta <= -round)
            return PickHeat.Reach;
        if (delta <= -round / 3.0)
            return PickHeat.MildReach;
        return PickHeat.Fair;
    }

    public static string Label(PickHeat heat) => heat switch
    {
        PickHeat.Steal => "steal",
        PickHeat.MildSteal => "slight steal",
        PickHeat.Fair => "fair",
        PickHeat.MildReach => "slight reach",
        PickHeat.Reach => "reach",
        PickHeat.Keeper => "keeper",
        PickHeat.Unknown => "no ADP",
        _ => ""
    };

    public static string Css(PickHeat heat) => heat switch
    {
        PickHeat.Steal => "steal",
        PickHeat.MildSteal => "mild-steal",
        PickHeat.Fair => "fair",
        PickHeat.MildReach => "mild-reach",
        PickHeat.Reach => "reach",
        PickHeat.Keeper => "keeper",
        PickHeat.Unknown => "unknown",
        _ => ""
    };
}
