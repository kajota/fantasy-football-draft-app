namespace FantasyDraftAssistant.Core.Ai;

public sealed record DraftGuidelinePreset(string Title, string Body);

public static class DraftGuidelinePresets
{
    public static readonly DraftGuidelinePreset HouseRules = new(
        "House rules",
        """
        Don't suggest a K or DEF until the last two rounds.
        Don't suggest a backup QB unless the value is clearly too good to pass (roughly a top-8 QB still on the board in a 1-QB league, or a starting-caliber QB2 in Superflex).
        Don't suggest a backup TE unless they are a clear smash over the other options.
        Prefer filling empty starting skill spots (RB/WR/TE, and QB if Superflex) over bench depth.
        """);

    public static readonly DraftGuidelinePreset BestAvailable = new(
        "Best available",
        """
        Take the best player available by rank/ADP unless a starter hole is urgent.
        Do not reach more than a few spots for need in the first six rounds.
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset ZeroRb = new(
        "Zero RB",
        """
        Avoid RB in the first 5–6 rounds unless an elite RB (roughly top 8) falls well past ADP.
        Load WR early. TE is fine if the value is real.
        Start taking RBs in the middle rounds for upside and volume.
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset HeroRb = new(
        "Hero RB",
        """
        Take one early RB if a top option is there, then stop drafting RBs until the middle rounds.
        After that first RB, prefer WR and value at TE.
        Do not take RB2 early just to "have two backs."
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset RbHeavy = new(
        "RB first",
        """
        Secure two starting RBs early (first three to four rounds) unless the board is WR-rich and the remaining RBs are a cliff.
        After two RBs, go best available among WR/TE (and QB if Superflex).
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset LateQb = new(
        "Late QB",
        """
        This is a 1-QB plan. Do not take a QB in the first 7–8 rounds unless an elite QB falls far past ADP.
        Spend early picks on RB/WR/TE.
        One QB is enough unless a backup is an obvious value.
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset SuperflexQb = new(
        "Superflex QB",
        """
        This is Superflex. Get one starting QB by the end of round 5 unless the remaining QB1s are gone and a smash skill player is falling.
        Strongly consider a second QB before the position cliffs (usually mid-draft).
        After two QBs, do not suggest a third unless the value is extreme.
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset TePremium = new(
        "TE premium",
        """
        If a top-tier TE is available near ADP in the first four to five rounds, take them before a replacement-level TE later.
        Do not reach a full round just to have a TE.
        After one starting TE, do not suggest a second unless the value is obvious.
        Still wait on K and DEF until the last two rounds.
        """);

    public static readonly DraftGuidelinePreset KeeperUpside = new(
        "Keeper upside",
        """
        This is a keeper league: each team may keep one player drafted in round 4 or later into next season.
        In roughly the last four rounds, lean toward high-upside rookies and young breakout candidates over safe veteran bench depth — a hit becomes next year's keeper at a late-round price.
        This is a tiebreaker, not an override: fill required starting slots first, and do not pass a clearly better player for a longshot.
        Still wait on K and DEF until the last two rounds.
        """);

    public static IReadOnlyList<DraftGuidelinePreset> All { get; } =
    [
        HouseRules,
        BestAvailable,
        ZeroRb,
        HeroRb,
        RbHeavy,
        LateQb,
        SuperflexQb,
        TePremium,
        KeeperUpside
    ];
}
