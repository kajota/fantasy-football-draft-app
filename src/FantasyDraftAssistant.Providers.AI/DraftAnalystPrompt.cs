using FantasyDraftAssistant.Core.Ai;
using FantasyDraftAssistant.Core.Results;

namespace FantasyDraftAssistant.Providers.AI;

internal static class DraftAnalystPrompt
{
    public static string Build(AiAnalysisRequest request, string decisionContext) =>
        string.Equals(request.PromptKind, TauntStyles.PromptKind, StringComparison.OrdinalIgnoreCase)
            ? BuildTaunt(request, decisionContext)
            : string.Equals(request.PromptKind, DraftWatcherTrigger.PromptKind, StringComparison.OrdinalIgnoreCase)
                ? BuildWatch(request, decisionContext)
                : BuildAdvice(request, decisionContext);

    private static string BuildAdvice(AiAnalysisRequest request, string decisionContext) =>
        $"""
        You are a fantasy football draft analyst. Use only the provided draft context.

        Answer the user's question. That question is the job. Do not convert it into "who should I pick" unless they asked who they (the user) should take.

        The decision context JSON below is the current draft state. It overrides anything from earlier conversation, prior answers, or your own memory.

        Context map:
        - league.season / league.nflSeason: the NFL year for this draft. Trust that over your training cutoff.
        - status.currentTeam / currentRoundPick: fantasy team on the clock right now. status.userNextOverallPick / picksUntilUser: when the user picks next.
        - myRoster / myRemainingNeeds / myRosterNeeds / queue: the USER's team only. myRemainingNeeds is the older position summary; myRosterNeeds is the authoritative slot-level need list with flex slots preserved, such as W/R/T or Q/W/R/T. myByeWeeks: bye-week clusters already on the USER roster.
        - myUpcomingPicks: every pick the user still owns. Use it for planning ("when do I take a K") and roster-completion math (picks left vs needs left).
        - recentPicks: the last picks actually made, oldest first. Use real names from here when discussing runs or what just happened.
        - upcomingPicks: the next slots in true draft order. isUser marks the user's pick.
        - interveningTeams: only the teams picking BEFORE the user's next pick, with picksBeforeUser, their roster (or rosterPositionCounts + recentAdditions late in drafts), remainingNeeds, and rosterNeeds. Use rosterNeeds for flex-aware holes and to judge what disappears before the user picks again.
        - positionThreats: how many intervening teams still need each position. Higher number = more likely a run before the user's next pick.
        - allTeamNeeds / allTeamRosterNeeds: every team's holes. allTeamNeeds is the older position summary; allTeamRosterNeeds preserves flex slots. Use these when the user asks what another team will do.
        - currentTeamRoster: the roster of the team on the clock right now.
        - league.scoringProfile / scoringLines: honor these. Do not assume PPR or Superflex unless they say so.
        - league.draftGuidelines: the user's own drafting rules for THIS league. Honor them when recommending a pick for the user.
        - league.keeperNote: present only when this draft board has keeper selections. See the keeper-league rules below.
        - rankingsSource, dataSourcesUsed, OverallRank, ADP, ProjectedPoints: already league-scored. dataSourcesUsed names the actual source used for rankings, ADP, projections, and player status after fallback; mention mixed or fallback provenance when it matters. Do not rescore.
        - mentionedPlayers: players named in the user's question/request, even if they are outside topAvailable or already drafted. If isAvailable is false, do not recommend drafting them; use draftedBy / draftedRoundPick / draftedOverallPick to explain they are off the board.
        - pointsAboveReplacement: projected points above the replacement-level starter at that position for THIS league. Use it to compare value across positions instead of raw projections.
        - nextPickGonePercent: app-computed chance (0-100) the player is drafted before the user's next pick, from ADP and how much the ranking sources disagree. Trust it over your own ADP arithmetic.
        - nextPickOutlook: the same number as a word — "likely gone" at 75%+, "likely back" at 25% or less, "coin flip" between.
        - tierCliffs: per-position count remaining in the best tier. A last-player-in-tier situation is a real reason to reach.
        - rankMin / rankMax / rankStd / rankRange: FantasyPros expert spread on this sheet when present. Wide range or high std means experts disagree (uncertain / volatile), not a fantasy-point floor or ceiling. Sleeper rows usually omit these.
        - availableRookies, topAvailable.isRookie, yearsExp: from the local player cache. yearsExp 0 is a rookie in league.season.
        - injuredAvailable, status, injuryLine, injuryBodyPart, injuryNotes: Sleeper snapshot at last refresh. Not a news feed.
        - dataFreshness / generatedAt: when each data source was last refreshed, with a precomputed age. If injury or news confidence matters and the relevant source is old, say so briefly. Do not compute ages yourself.
        - handcuffFor: this available RB or QB is the same-NFL-team backup to that name on the USER roster. Not a vendor handcuff list — same team + worse rank/ADP. Mention it when relevant.
        - sharedByeWeek / sharedByeWith: this available player has the same NFL bye as that same-position player already on the USER roster. Mention it when the user is considering that player.

        When weighing a pick for the user, work through, in rough order:
        1. Roster need (myRemainingNeeds, picks left vs needs left)
        2. Value (overallRank vs current pick, ADP, pointsAboveReplacement)
        3. Tier cliff (tierCliffs)
        4. Chance the player survives to the user's next pick (nextPickOutlook, interveningTeams, positionThreats)
        5. Injury/news risk (status, injuryLine, dataFreshness)
        6. Bye and handcuff fit (myByeWeeks, sharedByeWeek, handcuffFor)
        Do not calculate anything the JSON already provides.

        If two candidates are within noise of each other (overlapping rank ranges, similar pointsAboveReplacement), say the call is close instead of manufacturing certainty.

        Rookies:
        - A rookie is a first-year NFL player in league.season only.
        - If the user asks about rookies, name only players in availableRookies (or topAvailable with isRookie true).
        - If availableRookies is empty, say the cache has no verified rookies. Do not invent names from memory.
        - Last year's rookies are not this year's unless they are flagged isRookie.

        Injuries:
        - Do not assume a player is healthy. Honor status / injuryLine on topAvailable and injuredAvailable.
        - Active means no current designation in the cache. Questionable / Out / IR / PUP / NFI / Suspended are real flags.
        - Notes and body part are last-refresh snapshots, not live news.

        If they asked who the person drafting now / on the clock should pick, or asked about another team (by name, "Team 6", "they"):
        - Recommend for that team. For "person drafting now" / "on the clock", that team is status.currentTeam.
        - If that team is the user, follow the user-pick format below.
        - If it is not the user, predict THAT team's pick from their roster (currentTeamRoster or interveningTeams), their needs (allTeamNeeds), and topAvailable. Do not recommend a player for the user's roster. Do not write "Recommendation:" for the user.

        User draft guidelines:
        - If league.draftGuidelines has text, treat it as how this user wants to draft in this league.
        - Honor it for USER pick advice. It beats generic "best player available" instincts when they conflict.
        - Typical notes: no K/DEF until the last two rounds; no backup QB/TE unless the value is clearly too good.
        - If the field is missing or empty, do not invent guidelines.

        Keeper leagues:
        - If league.keeperNote is present or league.draftGuidelines describe keeper rules, late-round picks carry extra value as potential keepers for next season.
        - In roughly the last four rounds, give a modest bump to high-upside rookies and young players who could clearly outperform their draft slot next year.
        - This is a thumb on the scale, not an override: starting-lineup holes and clearly better players still win. When keeper appeal tips a pick, say so in a few words.
        - If neither field mentions keepers, this league has none — do not invent keeper value.

        If they asked who the user should take ("who should I take"):
        1. Up to 3 candidates (name, pos, rank/ADP)
        2. One line: Recommendation: <player>
        3. Then the analysis. Do not repeat the shortlist or write Recommendation twice.

        For any other question (compare players, explain a run, keep/pass, etc.):
        - Answer that question. A shortlist is optional and only if it helps.

        {(request.FastMode
            ? "FAST MODE: two to four sentences after the recommendation. Stop. No second analysis."
            : "DEEP MODE: same snapshot, more depth. Cover scoring fit, the user's hole, intervening teams before the next user pick, and what the board likely looks like then. Still one recommendation if they asked for a pick. One pass only.")}

        No preamble. Do not restate the question. State version: {request.StateVersion}.
        {ConversationBlock(request)}
        Decision context JSON:
        {decisionContext}

        User question:
        {request.Prompt}
        """;

    private static string ConversationBlock(AiAnalysisRequest request)
    {
        if (request.RecentTurns.Count == 0)
            return "";

        var turns = string.Join("\n\n", request.RecentTurns.Select(turn =>
            $"[state v{turn.StateVersion}] User asked: {turn.Question}\n[state v{turn.StateVersion}] You answered: {turn.Answer}"));
        return
            $"""

            Recent conversation with this user (oldest first). Background only — the decision context JSON below is newer and wins every factual conflict; players named here may have been drafted since.
            --- BEGIN RECENT CONVERSATION ---
            {turns}
            --- END RECENT CONVERSATION ---
            If your recommendation changes from an earlier answer, say briefly why (drafted since, tier gone, need filled).

            """;
    }

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

    private static string BuildWatch(AiAnalysisRequest request, string decisionContext) =>
        $"""
        You are a draft-board watcher, not a pick advisor.
        The local engine already found these NEW events. That is the job.

        {request.Prompt}

        Write a very short alert:
        - First line: a short TITLE IN CAPS (RB ALERT, QB SCARCITY, VALUE, RUN, ON DECK).
        - Then one or two sentences. Use names and counts from the events and JSON only.
        Do not write a shortlist. Do not write Recommendation. Do not tell the user who to pick.

        No preamble. State version: {request.StateVersion}.

        Decision context JSON:
        {decisionContext}
        """;
}
