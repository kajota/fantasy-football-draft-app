# AI Draft Advisor Context Plan

Living plan for improving the information and prompts sent to AI draft advisors.

## Status — 2026-08-18

First slice implemented, plus deterministic extras. `dotnet test` green (153 Core, 71 Data).

- New context fields: `recentPicks` (named, oldest first), `upcomingPicks` (with `isUser`), `myUpcomingPicks` (user's full remaining pick schedule), `interveningTeams` (only teams before the user's next pick; full roster while ≤7 players, otherwise position counts + last 3 additions), `currentTeamRoster`, `dataFreshness` (timestamps plus precomputed ages), `tierCliffs`, `positionThreats`, `myByeWeeks`, `generatedAt`, `status.userNextOverallPick`.
- Player annotations on available/queue rows: `nextPickOutlook` (likely gone / coin flip / likely back from ADP vs the user's next pick, band widened by rankStd) and `pointsAboveReplacement` (vs league-demand replacement baseline). Deterministic math lives in `Core/Analytics/DecisionContextMath.cs`.
- Available pool deepened to 200 (was 80) so `availableRookies` is no longer capped by the top of the board; AI slices unchanged in size.
- `interveningTeamNeeds` renamed to `allTeamNeeds` (it always contained every team). Prompt fixed (`ranksSource` → `rankingsSource`), explains all new fields, adds the decision rubric, close-call honesty, stale-data mention, and "current JSON beats conversation memory".
- Tests: `Core.Tests/DecisionContextMathTests.cs`, `Data.Tests/DecisionContextTests.cs`.

Second slice implemented 2026-08-18, plus keeper-league support:

- `AiAnalysisRequest.recentTurns` carries up to 3 of the same provider's prior advice turns (oldest first, answers trimmed to 1200 chars) into manual asks only — auto-ask, watch, and taunt turns neither send nor enter the window. Selection logic is `Core/Ai/ConversationWindow.cs`.
- `AiResponses` gained a `PromptKind` column (migration `010`) so taunt/watch turns are excluded from windows; advice turns store null.
- The prompt renders the window in a delimited block, tells the model the JSON wins every factual conflict, and asks it to briefly explain when its recommendation changes from an earlier turn.
- Keeper leagues: `league.keeperNote` appears in the decision context when the board has keeper selections; the prompt treats keeper upside as a late-round tiebreaker (bump high-upside rookies/young players in roughly the last four rounds, never over starting-lineup needs); a "Keeper upside" guideline preset covers the keep-one-from-round-4+ rule for League Setup.

Remaining slices below are unchanged: outside/live data (third), read-only AI tools (later).

## Goal

Make connected AI advisors better at live fantasy football draft help by giving them the same practical context a human draft assistant would use: league rules, scoring, roster state, draft flow, opponent needs, player value, injury freshness, and the user's own draft preferences.

## Current State

- AI requests use one static decision-context JSON blob plus the user's question.
- The prompt is already domain-aware and tells the model to honor league scoring, user guidelines, rookies, injuries, handcuffs, byes, and fast/deep mode.
- The app already has read-only query services for roster, available players, draft board, recent picks, upcoming teams, and player details.
- The decision context currently includes top available players, user roster, user needs, queue, position counts, tiers, recent position run, alerts, rookies, injuries, rankings source, and league settings.

## Main Gaps

- Recent picks are summarized only as positions, not actual player/team/pick names.
- Upcoming draft order is not included in the AI blob.
- `interveningTeamNeeds` currently sends all teams, not just teams picking before the user's next pick.
- Opponent roster details are missing, so predictions about another team are weaker.
- Follow-up questions do not include prior conversation turns.
- Data refresh times are missing, especially for rankings, ADP, projections, and injury/player status.
- Live clock urgency is not sent to the AI.
- Available rookies are drawn from the top available slice, which can hide later rookie options.

## First Implementation Slice

Add richer static JSON context before adding provider-specific AI tools.

### Files To Edit

Primary files:

- `src/FantasyDraftAssistant.Core/Query/DraftDtos.cs`
- `src/FantasyDraftAssistant.Data/Services/DraftQueryService.cs`
- `src/FantasyDraftAssistant.Providers.AI/DraftAnalystPrompt.cs`

Likely test files:

- `tests/FantasyDraftAssistant.Data.Tests/*`
- `tests/FantasyDraftAssistant.Core.Tests/*`

Only edit UI files if a compile error or a clearly useful display update requires it.

### DTO Additions

Add these to `DecisionContextDto` in `src/FantasyDraftAssistant.Core/Query/DraftDtos.cs`:

- `recentPicks`: last 10-15 picks with player, team, position, NFL team, round/pick, overall pick, source.
- `upcomingPicks`: next 8-12 undrafted slots in actual draft order.
- `interveningTeams`: teams picking before the user's next pick, with pick count, roster, and remaining needs.
- `currentTeamRoster`: roster for the team currently on the clock.
- `dataFreshness`: refresh timestamps/counts for rankings, ADP, projections, and player status sources.

Suggested new DTOs:

```csharp
public sealed class UpcomingPickDto
{
    public required int OverallPick { get; init; }
    public required string RoundPick { get; init; }
    public required string Team { get; init; }
    public string TeamId { get; init; } = "";
    public bool IsUser { get; init; }
}

public sealed class InterveningTeamDto
{
    public required string TeamName { get; init; }
    public string TeamId { get; init; } = "";
    public required int PicksBeforeUser { get; init; }
    public required IReadOnlyList<RosterPlayerDto> Roster { get; init; }
    public required IReadOnlyList<string> RemainingNeeds { get; init; }
}

public sealed class DataFreshnessDto
{
    public required IReadOnlyList<DataFreshnessItemDto> Sources { get; init; }
}

public sealed class DataFreshnessItemDto
{
    public required string ProviderKey { get; init; }
    public required string Dataset { get; init; }
    public required string RefreshedAt { get; init; }
    public int RecordCount { get; init; }
}
```

Serialization notes:

- `DraftJson` uses camelCase property names.
- Null properties are omitted.
- New fields should be additive so old saved AI responses still load normally.
- Prefer empty arrays over null lists in DTOs.

### Query Service Work

Update `GetDecisionContextAsync` in `src/FantasyDraftAssistant.Data/Services/DraftQueryService.cs` to:

- Reuse `GetRecentPicksAsync(context, 12)` for `recentPicks`.
- Build `upcomingPicks` from unselected `state.Slots`.
- Find the user's next pick and include only intervening teams before that pick.
- Include repeated intervening teams once, with `PicksBeforeUser` counting how many picks they have before the user.
- Include the current on-clock roster when `snapshot.CurrentTeamId` is present.
- Load `fantasyData.GetRefreshInfoAsync` and serialize it into `dataFreshness`.

Suggested implementation order:

1. Add the DTOs and new `DecisionContextDto` properties.
2. Add private helper methods in `DraftQueryService` for upcoming picks, intervening teams, roster mapping, and freshness mapping.
3. Populate the new fields from `GetDecisionContextAsync`.
4. Update the prompt context map.
5. Add or update tests.
6. Run `dotnet test`.

Important behavior details:

- `recentPicks` should exclude future/unselected slots and include keeper selections if they are active on the board.
- `upcomingPicks` should include only unselected slots, ordered by `OverallPick`.
- If the user is currently on the clock, `interveningTeams` should be empty.
- If the user has no remaining pick, `interveningTeams` should be empty.
- If no user team is set, preserve the current fallback behavior of using the first team.
- If the draft is complete, `upcomingPicks` and `interveningTeams` should be empty.
- If the same opponent picks multiple times before the user's next pick, include that team once and set `PicksBeforeUser` to the count of those picks.
- `currentTeamRoster` should be null only when there is no current team, such as a completed draft.
- `dataFreshness.sources` should be empty when no refresh records exist.
- Do not add full rosters for every team in the first slice. Limit opponent roster detail to the current team and teams picking before the user.

### Prompt Updates

Update `src/FantasyDraftAssistant.Providers.AI/DraftAnalystPrompt.cs`:

- Rename prompt text from `ranksSource` to `rankingsSource`.
- Explain `recentPicks`, `upcomingPicks`, `interveningTeams`, `currentTeamRoster`, and `dataFreshness`.
- Tell the model to use actual upcoming pick order when judging whether a player may make it back.
- Tell the model to mention stale/missing data briefly when injury or news confidence matters.
- Add a short decision rubric:
  - roster need
  - rank/ADP value
  - tier cliff
  - projected points
  - injury/news risk
  - bye/handcuff fit
  - chance the player survives to the user's next pick

Prompt constraints:

- Keep the existing "answer the user's actual question" behavior.
- Keep the existing fast/deep distinction.
- Keep the "use only provided draft context" rule.
- Make current JSON fields authoritative over prior conversation or model memory.
- Avoid asking the model to calculate anything the app can calculate locally.

### Tests

Add focused tests for:

- Decision context includes recent pick player names.
- Upcoming picks are ordered by overall pick.
- Intervening teams stop before the user's next pick.
- Current team roster is included when another team is on the clock.
- Data freshness appears when refresh records exist.

Run:

```bash
dotnet test
```

## Definition Of Done For First Slice

- `DecisionContextDto` includes richer live-draft context fields.
- Serialized AI context JSON includes:
  - last 12 named picks
  - next 8-12 upcoming picks
  - only teams picking before the user's next pick
  - current on-clock team's roster when applicable
  - source freshness rows when available
- Prompt text explains each new context field.
- Existing AI providers still compile and use the same provider-neutral prompt builder.
- Focused tests cover the new context behavior.
- `dotnet test` passes.

## Non-Goals For First Slice

- Do not add provider-specific AI tool/function calling yet.
- Do not add Yahoo live draft sync yet.
- Do not add new external data providers yet.
- Do not redesign the AI settings UI.
- Do not change draft mutation behavior.
- Do not let AI calls draft, undo, correct, or sync picks.

## Second Slice

Improve conversational continuity.

- Add a small recent-turn window to `AiAnalysisRequest`.
- Include 2-4 prior turns in the prompt with clear delimiters.
- Keep the current draft-state JSON authoritative over older conversation history.
- Use this only for manual asks, not necessarily auto-ask/watch events.

## Third Slice

Add stronger outside/live information.

- Yahoo live draft pick sync once API access is available.
- Yahoo/platform draft-room rank if available.
- Player news and injury updates with timestamps.
- Sleeper trending adds/drops as a lightweight market signal.
- Deterministic local scarcity and tier-cliff summaries.

## Later Option: Read-Only AI Tools

After richer static context is working, optionally expose provider-specific read-only tools backed by `IDraftQueryService`:

- `get_available_players`
- `get_team_roster`
- `get_recent_picks`
- `get_upcoming_picks`
- `get_player_details`

These tools must not mutate draft state. They should not draft players, undo picks, edit rosters, or change Yahoo sync state.

## Notes

- Keep deterministic calculations local when possible. The AI should interpret and explain; the app should count, sort, and calculate.
- Prefer enriching the existing JSON first because it is simpler, faster, and provider-neutral.
- Revisit token cost after adding richer context, especially for multiple providers running in parallel.
