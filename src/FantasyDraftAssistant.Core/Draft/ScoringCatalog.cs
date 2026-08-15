using FantasyDraftAssistant.Core.Enums;
using FantasyDraftAssistant.Core.Ids;
using FantasyDraftAssistant.Core.Models;

namespace FantasyDraftAssistant.Core.Engine;

public sealed record ScoringCategoryInfo(
    ScoringCategory Category,
    string Group,
    string Label,
    string Help,
    decimal DefaultPoints,
    string Unit);

public static class ScoringCatalog
{
    private static readonly ScoringCategory[] ShortFieldGoalBands =
    [
        ScoringCategory.FieldGoal0To19,
        ScoringCategory.FieldGoal20To29,
        ScoringCategory.FieldGoal30To39
    ];

    private static readonly ScoringCategory[] FieldGoalBands =
    [
        ScoringCategory.FieldGoal0To19,
        ScoringCategory.FieldGoal20To29,
        ScoringCategory.FieldGoal30To39,
        ScoringCategory.FieldGoal40To49,
        ScoringCategory.FieldGoal50Plus
    ];

    public static IReadOnlyList<ScoringCategoryInfo> All { get; } =
    [
        new(ScoringCategory.PassingYard, "Passing", "Passing yards",
            "Points per passing yard. 0.04 is 1 point per 25 yards (Yahoo's “25 yards per point”). This is not a passing TD.",
            0.04m, "per yard"),
        new(ScoringCategory.PassingTouchdown, "Passing", "Passing touchdown",
            "Points for each passing touchdown. Common values are 4 or 6. This is not per yard.",
            4m, "each"),
        new(ScoringCategory.Interception, "Passing", "Interception thrown",
            "Points for each interception the QB throws. Usually −2. A negative number is a penalty.",
            -2m, "each"),
        new(ScoringCategory.RushingYard, "Rushing", "Rushing yards",
            "Points per rushing yard. 0.10 is 1 point per 10 yards (Yahoo's “10 yards per point”). This is not a rushing TD.",
            0.10m, "per yard"),
        new(ScoringCategory.RushingTouchdown, "Rushing", "Rushing touchdown",
            "Points for each rushing touchdown. Usually 6. This is not per yard.",
            6m, "each"),
        new(ScoringCategory.Reception, "Receiving", "Reception (PPR)",
            "Points per catch. This is PPR, not yards. 0 = standard (no PPR). 0.5 = half PPR. 1 = full PPR. Yahoo's “10 yards per point” is the Receiving yards row below.",
            0.50m, "per catch"),
        new(ScoringCategory.ReceivingYard, "Receiving", "Receiving yards",
            "Points per receiving yard. 0.10 is 1 point per 10 yards (Yahoo's “10 yards per point”). This is not PPR — PPR is the Reception row above.",
            0.10m, "per yard"),
        new(ScoringCategory.ReceivingTouchdown, "Receiving", "Receiving touchdown",
            "Points for each receiving touchdown. Usually 6. This is not per yard.",
            6m, "each"),
        new(ScoringCategory.FumbleLost, "Turnovers", "Fumble lost",
            "Points for each fumble lost. Usually −2. A negative number is a penalty.",
            -2m, "each"),
        new(ScoringCategory.TwoPointConversion, "Turnovers", "Two-point conversion",
            "Points for each two-point conversion (pass, rush, or catch). Usually 2.",
            2m, "each"),
        new(ScoringCategory.FieldGoal0To19, "Kicking", "Field goal 0–19 yards",
            "Points for each made field goal from 0–19 yards. Yahoo default is 3.",
            3m, "each"),
        new(ScoringCategory.FieldGoal20To29, "Kicking", "Field goal 20–29 yards",
            "Points for each made field goal from 20–29 yards. Yahoo default is 3.",
            3m, "each"),
        new(ScoringCategory.FieldGoal30To39, "Kicking", "Field goal 30–39 yards",
            "Points for each made field goal from 30–39 yards. Yahoo default is 3.",
            3m, "each"),
        new(ScoringCategory.FieldGoal40To49, "Kicking", "Field goal 40–49 yards",
            "Points for each made field goal from 40–49 yards. Yahoo default is 4.",
            4m, "each"),
        new(ScoringCategory.FieldGoal50Plus, "Kicking", "Field goal 50+ yards",
            "Points for each made field goal from 50 yards or longer. Yahoo default is 5.",
            5m, "each"),
        new(ScoringCategory.ExtraPoint, "Kicking", "Extra point (PAT)",
            "Points for each made extra point / point-after-touchdown. Yahoo default is 1.",
            1m, "each"),
        new(ScoringCategory.ExtraPointReturned, "Kicking", "Extra point returned",
            "Points when a blocked extra point is returned (usually awarded to the DST). Yahoo default is 2. This is not a made PAT.",
            2m, "each"),
        new(ScoringCategory.Sack, "Defense", "Sack",
            "Points awarded to the DST for each sack. Usually 1.",
            1m, "each"),
        new(ScoringCategory.DefensiveInterception, "Defense", "Interception",
            "Points awarded to the DST for each interception. Usually 2.",
            2m, "each"),
        new(ScoringCategory.FumbleRecovery, "Defense", "Fumble recovery",
            "Points awarded to the DST for each fumble recovery. Usually 2.",
            2m, "each"),
        new(ScoringCategory.DefensiveTouchdown, "Defense", "Defensive / return TD",
            "Points awarded to the DST for each defensive or special-teams touchdown. Usually 6.",
            6m, "each"),
        new(ScoringCategory.Safety, "Defense", "Safety",
            "Points awarded to the DST for each safety. Usually 2.",
            2m, "each"),
        new(ScoringCategory.PointsAllowed0, "Defense", "Points allowed 0",
            "Bonus points for the DST when the opponent scores 0. Yahoo default is 10.",
            10m, "when it happens"),
        new(ScoringCategory.PointsAllowed1To6, "Defense", "Points allowed 1–6",
            "Bonus points for the DST when the opponent scores 1–6. Yahoo default is 7.",
            7m, "when it happens"),
        new(ScoringCategory.PointsAllowed7To13, "Defense", "Points allowed 7–13",
            "Bonus points for the DST when the opponent scores 7–13. Yahoo default is 4.",
            4m, "when it happens"),
        new(ScoringCategory.PointsAllowed14To20, "Defense", "Points allowed 14–20",
            "Bonus points for the DST when the opponent scores 14–20. Yahoo default is 1.",
            1m, "when it happens"),
        new(ScoringCategory.PointsAllowed21To27, "Defense", "Points allowed 21–27",
            "Points for the DST when the opponent scores 21–27. Yahoo default is 0.",
            0m, "when it happens"),
        new(ScoringCategory.PointsAllowed28To34, "Defense", "Points allowed 28–34",
            "Points for the DST when the opponent scores 28–34. Yahoo default is −1 (a penalty).",
            -1m, "when it happens"),
        new(ScoringCategory.PointsAllowed35Plus, "Defense", "Points allowed 35+",
            "Points for the DST when the opponent scores 35 or more. Yahoo default is −4 (a penalty).",
            -4m, "when it happens")
    ];

    public static IReadOnlyList<ScoringPreset> Standard() => WithReception(0m, passingTouchdown: 4m);

    public static IReadOnlyList<ScoringPreset> HalfPpr() => WithReception(0.5m, passingTouchdown: 4m);

    public static IReadOnlyList<ScoringPreset> Ppr() => WithReception(1m, passingTouchdown: 4m);

    public static string Line(ScoringCategory category, decimal points)
    {
        var info = All.FirstOrDefault(item => item.Category == category);
        if (info is null)
            return $"{category}: {points.ToString("0.##")}";
        return $"{info.Label}: {points.ToString("0.##")} {info.Unit}";
    }

    public static decimal Resolve(ScoringCategoryInfo info, IReadOnlyDictionary<ScoringCategory, decimal> saved)
    {
        if (saved.TryGetValue(info.Category, out var points))
            return points;

        var hasBands = FieldGoalBands.Any(saved.ContainsKey);
        if (!hasBands
            && ShortFieldGoalBands.Contains(info.Category)
            && saved.TryGetValue(ScoringCategory.FieldGoal, out var flat))
        {
            return flat;
        }

        return info.DefaultPoints;
    }

    public static IReadOnlyList<ScoringRule> Complete(LeagueId leagueId, IReadOnlyList<ScoringRule> saved)
    {
        var points = saved.ToDictionary(rule => rule.Category, rule => rule.Points);
        var byCategory = saved.ToDictionary(rule => rule.Category);
        return All.Select(info =>
        {
            if (byCategory.TryGetValue(info.Category, out var existing))
                return existing;
            return new ScoringRule
            {
                ScoringRuleId = ScoringRuleId.New(),
                LeagueId = leagueId,
                Category = info.Category,
                Points = Resolve(info, points)
            };
        }).ToList();
    }

    public static string ProfileName(IReadOnlyList<ScoringRule> rules, bool superflex)
    {
        var reception = rules.FirstOrDefault(r => r.Category == ScoringCategory.Reception)?.Points ?? 0m;
        var ppr = reception switch
        {
            < 0.25m => "Standard",
            < 0.75m => "Half PPR",
            _ => "PPR"
        };
        return superflex ? $"{ppr} Superflex" : $"{ppr} 1-QB";
    }

    private static IReadOnlyList<ScoringPreset> WithReception(decimal reception, decimal passingTouchdown) =>
        All.Select(info =>
        {
            var points = info.Category switch
            {
                ScoringCategory.Reception => reception,
                ScoringCategory.PassingTouchdown => passingTouchdown,
                _ => info.DefaultPoints
            };
            return new ScoringPreset(info.Category, points);
        }).ToList();
}
