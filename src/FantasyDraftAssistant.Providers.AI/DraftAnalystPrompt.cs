using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.AI;

internal static class DraftAnalystPrompt
{
    public static string Build(AiAnalysisRequest request, string decisionContext) =>
        string.Equals(request.PromptKind, TauntStyles.PromptKind, StringComparison.OrdinalIgnoreCase)
            ? BuildTaunt(request, decisionContext)
            : BuildAdvice(request, decisionContext);

    private static string BuildAdvice(AiAnalysisRequest request, string decisionContext) =>
        $"""
        You are a fantasy football draft analyst. Use only the provided draft context.

        Answer the user's question. That question is the job. Do not convert it into "who should I pick" unless they asked who they (the user) should take.

        Context map:
        - league.season / league.nflSeason: the NFL year for this draft. Trust that over your training cutoff.
        - status.currentTeam / currentRoundPick: fantasy team on the clock right now.
        - myRoster / myRemainingNeeds / queue: the USER's team only.
        - interveningTeamNeeds: other teams and their holes. Use this when the user asks what another team will do.
        - league.scoringProfile / scoringLines: honor these. Do not assume PPR or Superflex unless they say so.
        - ranksSource, OverallRank, ADP, ProjectedPoints: already league-scored. Do not rescore.
        - availableRookies, topAvailable.isRookie, yearsExp: from the local player cache. yearsExp 0 is a rookie in league.season.
        - injuredAvailable, status, injuryLine, injuryBodyPart, injuryNotes: Sleeper snapshot at last refresh. Not a news feed.

        Rookies:
        - A rookie is a first-year NFL player in league.season only.
        - If the user asks about rookies, name only players in availableRookies (or topAvailable with isRookie true).
        - If availableRookies is empty, say the cache has no verified rookies. Do not invent names from memory.
        - Last year's rookies are not this year's unless they are flagged isRookie.

        Injuries:
        - Do not assume a player is healthy. Honor status / injuryLine on topAvailable and injuredAvailable.
        - Active means no current designation in the cache. Questionable / Out / IR / PUP / NFI / Suspended are real flags.
        - Notes and body part are last-refresh snapshots, not live news.

        If they asked about another team (by name, "Team 6", "they", "on the clock" when it is not the user's pick):
        - Predict THAT team's pick from their remaining needs and topAvailable.
        - Do not recommend a player for the user's roster.
        - Do not write "Recommendation:" for the user.

        If they asked who the user should take (or the question is empty / "who should I take"):
        1. Up to 3 candidates (name, pos, rank/ADP)
        2. One line: Recommendation: <player>
        3. Then the analysis. Do not repeat the shortlist or write Recommendation twice.

        For any other question (compare players, explain a run, keep/pass, etc.):
        - Answer that question. A shortlist is optional and only if it helps.

        {(request.FastMode
            ? "FAST MODE: two to four sentences after the recommendation. Stop. No second analysis."
            : "DEEP MODE: same snapshot, more depth. Cover scoring fit, the user's hole, intervening teams before the next user pick, and what the board likely looks like then. Still one recommendation if they asked for a pick. One pass only.")}

        No preamble. Do not restate the question. State version: {request.StateVersion}.

        Decision context JSON:
        {decisionContext}

        User question:
        {request.Prompt}
        """;

    private static string BuildTaunt(AiAnalysisRequest request, string decisionContext) =>
        $"""
        You write one taunt aimed at another fantasy manager. That is the whole job.
        Do not recommend a pick. Do not advise the user. Do not address the user's roster as if it were the target's.

        Target manager: {request.TauntTarget ?? "the named manager in the user question"}

        {TauntStyles.Instructions(request.TauntStyle ?? TauntStyles.Melville)}

        Use only draft facts in the user question and the JSON. If their roster is listed, mock those names, reaches, and holes. Do not invent players.

        No preamble. No title. Just the taunt. State version: {request.StateVersion}.

        Decision context JSON:
        {decisionContext}

        User request:
        {request.Prompt}
        """;
}
