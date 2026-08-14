# Fantasy Draft Assistant
## Software Requirements Specification

**Version:** 0.4
**Date:** 2026-08-12
**Status:** Revised requirements baseline — clarifies that manual entry alongside Yahoo Synchronized mode is routine, not only failure-driven, and that reconciliation conflicts resolve through the existing rollback/correction mechanism rather than a separate code path. Otherwise retains all v0.3 resilient external-provider architecture, local fantasy-data caching, Yahoo/manual draft failover and reconciliation, and draft-day readiness checks, plus v0.2 queue, Superflex, rollback/redo, spending-cap, and post-draft requirements.

---

## 1. Purpose

Fantasy Draft Assistant is a desktop application for managing, monitoring, analyzing, and assisting with fantasy football drafts.

The application is intended for leagues hosted by Yahoo Fantasy Football and shall support:

- Yahoo-hosted online drafts.
- In-person drafts for leagues that are otherwise managed through Yahoo.
- Keeper leagues, limited to a single keeper per team (see Section 11).
- Mock drafts and practice drafts.
- Custom league scoring and roster rules, including Superflex and multi-QB formats.
- Multiple AI providers for live draft analysis and recommendations.

The application is specifically a **draft assistant**. It is not intended to replace Yahoo as a complete fantasy football league-management platform, nor is it intended to recreate a complete commercial draft product such as FantasyPros Draft Wizard.

The primary objective is to maintain an accurate, durable local representation of the live draft and give one or more AI assistants structured access to that information so that useful recommendations can be made without repeatedly explaining league rules, roster status, available players, or previous selections.

Yahoo and external fantasy-data services shall be treated as replaceable input providers. The application shall cache the data it needs locally and shall remain capable of recording and managing a draft when one or more external services are unavailable.

---

## 2. Primary Goals

The application shall:

1. Import league configuration from Yahoo whenever practical.
2. Allow all imported league information to be reviewed and manually corrected.
3. Maintain the complete authoritative application draft state locally in real time.
4. Support both automatic Yahoo draft monitoring and fast manual draft entry.
5. Allow immediate manual continuation of a Yahoo-hosted draft if Yahoo synchronization fails.
6. Support keeper leagues limited to one keeper per team.
7. Obtain player metadata, rankings, projections, ADP, tiers, and related information from replaceable external fantasy-data providers where available.
8. Cache required fantasy data locally before and during a draft so external data-provider availability is not required to continue drafting.
9. Calculate deterministic draft information locally rather than relying on AI for bookkeeping.
10. Expose structured draft information to multiple AI providers.
11. Allow multiple AI providers to analyze the same draft independently.
12. Optimize AI interaction for low latency during timed online drafts.
13. Continuously persist draft state so a crash does not require reconstructing the draft.
14. Support rollback to any previous draft selection, and redo of a rolled-back action.
15. Support alternate draft branches for mock drafts and "what-if" analysis.
16. Maintain a user-managed draft queue to speed up selection during live drafts.
17. Explicitly support Superflex and multi-QB roster formats, including QB-aware scarcity analysis.
18. Track and display time remaining on the current pick where that information is available.
19. Provide draft-source health, synchronization, fallback, and reconciliation information during Yahoo-hosted drafts.
20. Provide a pre-draft readiness check for local data, external integrations, credentials, and recovery state.
21. Enforce configurable AI spending limits so live-draft AI usage cannot run up an unbounded bill.
22. Generate a post-draft grade and recap using deterministic analytics and connected AI providers.
23. Provide a polished, professional desktop interface.
24. Run as a first-class application on Omarchy Linux.
25. Support Windows where practical without compromising the Linux application.

---

## 3. Non-Goals

The initial application shall not attempt to provide:

- Weekly fantasy scoring.
- Weekly lineup management.
- Waiver-wire management.
- Trades.
- League standings.
- Head-to-head matchup management.
- Playoff management.
- Message boards.
- League dues management.
- NFL game tracking beyond information useful to draft decisions.
- Auction drafts. All supported draft formats are pick-based (Snake, Linear, Custom); dollar-bidding mechanics are out of scope.
- Dynasty startup drafts and rookie-only drafts. Keeper support is limited to a single carried-over player per team (see Section 11); the application does not model dynasty rosters, future-pick valuation, or taxi squads.
- Automatic live-draft synchronization with platforms other than Yahoo (e.g., ESPN, Sleeper, CBS, NFL.com).

Yahoo or the league's normal fantasy platform remains responsible for league operation after the draft.

---

# 4. Platform Requirements

## 4.1 Primary Platform

The primary development and runtime environment shall be:

- Omarchy Linux.
- Hyprland/Wayland desktop environment.
- x86-64 desktop hardware.

Linux shall be treated as a first-class platform rather than as a secondary port.

## 4.2 Secondary Platform

The application should also support:

- Windows 11.
- x86-64 desktop hardware.

The intent is that the application could eventually be distributed to another user on Windows without requiring a separate codebase.

Cross-platform support is desirable but shall not override major usability, reliability, or maintainability advantages on Linux.

## 4.3 Distribution

The application should be distributable as a self-contained application that does not require the user to manually install the .NET runtime.

Platform-specific packages or executables may be produced from the same source tree.

---

# 5. Proposed Technology Stack

The initial technical direction is:

```text
Language                 C#
Runtime                  .NET
Desktop UI               Avalonia UI
UI Architecture          MVVM
Database                 SQLite
Networking               .NET HTTP APIs
Yahoo Integration        Yahoo Fantasy Sports API adapter
Fantasy Data Integration Provider adapters with local cache
AI Integration           Provider-specific API adapters
Optional External        MCP interface
```

The application architecture shall keep the following concerns separate:

```text
User Interface
Draft Engine
Draft Persistence
Yahoo League Import
Yahoo Live Draft Source
Manual Draft Source
Fantasy Data Provider Integration
Local Player/Fantasy Data Cache
Analytics
AI Provider Integration
External MCP Interface
```

The draft engine shall consume common internal models and interfaces rather than provider-specific response objects.

No Yahoo-, fantasy-data-provider-, or AI-provider-specific implementation shall be embedded directly into the core draft engine.

---

# 6. League Management

The application shall support multiple fantasy leagues.

Each league shall maintain its own configuration and draft history.

Stored league information shall include at least:

- League name.
- Fantasy platform.
- Yahoo league identifier when applicable.
- Season.
- Number of teams.
- Team names.
- Owner names or user-defined team labels.
- User's team.
- Draft type.
- Draft order.
- Number of rounds.
- Total roster size.
- Starting roster requirements.
- Bench size.
- IR positions when relevant.
- Flex-position eligibility, including whether QB is flex-eligible (Superflex/multi-QB).
- Scoring rules.
- Keeper information.
- Draft source.

---

# 7. Yahoo League Import

## 7.1 Import Purpose

Yahoo shall be used to populate as much league information as practical.

Imported information may include:

- League identity.
- Number of teams.
- Team names.
- Team ownership information.
- Roster configuration.
- Scoring configuration.
- Draft configuration.
- Keeper information where available and reliable.
- Other useful league metadata.

## 7.2 Yahoo Authentication

Yahoo integration shall use the supported Yahoo authentication mechanism.

Authentication credentials and tokens shall be stored securely and shall not be stored in plaintext in the primary application database.

## 7.3 Import Review

Information obtained from Yahoo shall not automatically be treated as correct.

After import, the application shall present a League Setup Review.

The review should prominently identify settings commonly requiring manual verification, including:

- Draft order.
- Keeper assignments.
- Keeper round costs.
- Team names.
- Team-owner mappings.
- Custom roster rules.

## 7.4 Manual Overrides

The user shall be able to manually override imported Yahoo information.

Manual overrides shall remain intact unless the user explicitly chooses to replace them during a subsequent Yahoo import.

A Yahoo refresh shall not silently overwrite manually corrected draft order or keeper information.

---

# 8. Roster Configuration

The application shall support arbitrary roster definitions.

Example:

```text
QB       1
RB       2
WR       2
TE       1
FLEX     1
K        1
DEF      1
BENCH    6
IR       2
```

The application shall distinguish between:

- Required starting positions.
- Flexible starting positions.
- Bench positions.
- Inactive positions such as IR.

Flexible positions shall specify eligible player positions.

Example:

```text
FLEX = RB / WR / TE
```

## 8.1 Superflex and Multi-QB Support

Superflex and 2-QB leagues shall be fully supported using the same flexible-position mechanism described above, without requiring separate application logic. A Superflex slot is simply a flex position whose eligible positions include QB.

Example:

```text
FLEX = QB / RB / WR / TE
```

The application shall not treat QB as a special case artificially restricted to a single required starting slot. Any number of QB-eligible starting slots (required or flexible) shall be supported.

Because Superflex and multi-QB leagues dramatically increase QB scarcity, the application shall recognize when a league's roster configuration creates elevated QB demand and shall reflect that in:

- Deterministic positional-value and scarcity analytics (Section 34).
- Position-run and value alerts (Section 37).
- AI-visible decision context (Section 45), so AI recommendations reason about QB correctly rather than assuming a standard 1-QB league.

The system shall know:

- Total players each fantasy team must draft.
- Which starting positions are required.
- Which positions qualify for flexible roster slots.
- How many bench selections are available.
- Which roster requirements remain unfilled during the draft.

---

# 9. Scoring System

The complete scoring system shall be stored independently for each league.

The league shall not merely be classified as:

```text
Standard
Half PPR
Full PPR
```

The actual point values shall be retained.

Example:

```text
Passing yard           0.04
Passing TD             4
Interception          -2

Rushing yard           0.10
Rushing TD             6

Reception              1
Receiving yard         0.10
Receiving TD           6
```

The system shall support league-specific scoring modifiers and bonus categories where possible.

Imported player projections shall be converted into projected fantasy points using the selected league's actual scoring rules.

---

# 10. Draft Order

The application shall maintain an explicit sequence of draft selections.

Supported draft formats shall initially include:

- Snake.
- Linear.
- Custom.

Auction drafts are explicitly out of scope (see Section 3).

The application shall generate and store actual draft slots rather than relying exclusively on a mathematical calculation during the draft.

Example:

```text
1.01    Bob
1.02    Steve
1.03    Kelly
...
2.01    Dave
2.02    Mike
...
```

Explicit draft slots provide future support for:

- Traded draft picks.
- Lost selections.
- Keeper selections.
- Nonstandard orders.
- Other custom rules.

The draft order shall always be manually editable before a draft begins.

---

# 11. Keepers

Keepers shall be first-class draft objects.

Each fantasy team shall be limited to **at most one keeper**. The application shall enforce this limit during keeper entry, and shall flag any imported Yahoo keeper data that implies more than one keeper for a team so it can be manually reviewed and corrected. This limit exists because the league this application is built for uses a single-keeper format; broader dynasty-style keeper rules are out of scope (see Section 3).

Each keeper shall contain at least:

- Player.
- Fantasy team.
- Keeper round.
- Actual draft slot consumed.
- Optional notes.

Example:

```text
Team:       Kelly
Player:     Josh Allen
Round Cost: 5
Draft Slot: 5.04
```

Keeper players shall be unavailable to other teams before live drafting begins.

Keeper selections shall appear on the draft board in their actual draft positions.

The application shall support manually correcting imported keeper information.

---

# 12. Player Database

The application shall maintain a locally cached player database independent of any individual league.

The player database is application infrastructure, not a manually maintained fantasy knowledge base. It should normally be populated and refreshed from Yahoo and/or one or more configured external fantasy-data providers.

Each player should contain at least:

- Internal application player ID.
- Name.
- NFL team.
- Position.
- Bye week.
- Current player status (see Section 13).
- External provider identifiers where known.

External identifiers may include:

- Yahoo player ID.
- FantasyPros identifier.
- Other ranking-provider identifiers.
- Projection-provider identifiers.

The application shall provide a mechanism for identifying and resolving player-record mismatches between providers.

Provider-specific identifiers shall remain mapping metadata. Core draft records shall reference the application's internal player identifier so changing a data provider does not require redesigning draft history.

---

# 13. Player Status and Injury Information

Each player's current status shall be represented using a defined, enumerated set of values rather than free text, at minimum:

```text
Active
Questionable
Doubtful
Out
Injured Reserve (IR)
Physically Unable to Perform (PUP)
Suspended
Non-Football Injury (NFI)
```

Additional practice-participation detail (e.g., did-not-practice / limited / full) may be retained where available but is not required for the initial implementation.

## 13.1 Status Source

Yahoo may serve as the initial primary source for player status because it is already an integrated league source and is available for every player. The design shall not require Yahoo specifically for status information, however; a fantasy-data provider or dedicated injury/news provider may replace or supplement it through the same provider architecture described in Section 16.

The most recently retrieved player status shall be cached locally so loss of the status provider during a draft does not remove previously known status information.

## 13.2 Refresh and Staleness

Player status shall be refreshed at least whenever the player database is refreshed from Yahoo, and on demand during draft preparation.

The UI shall indicate when status data was last refreshed so the user can judge how current it is, particularly late in draft week when status changes frequently.

## 13.3 Presentation

Status shall be visibly surfaced:

- As an indicator (icon, tag, or color) in the available-player list (Section 55).
- In full in player-detail views.
- In AI-visible player details (`GetPlayerDetails`), so AI recommendations can factor in injury risk without a separate lookup.

---

# 14. Rankings

The application shall support rankings obtained from one or more external fantasy-data providers.

Each cached ranking record shall include:

- Player.
- Ranking source.
- Overall rank.
- Position rank where available.
- Tier where available.
- Source-data timestamp where available.
- Local retrieval/cache timestamp.

Multiple ranking sources may coexist.

The UI should permit selecting a preferred ranking source while retaining other cached rankings for comparison.

Ranking-provider integration shall remain modular so sources can be replaced or added without changing the draft engine.

The initial product does **not** require building a proprietary player-ranking system. Existing expert or consensus rankings are inputs to the application's analytics and AI decision context.

File-based ranking import may be supported as a fallback or advanced feature, but a large generalized ranking-file import subsystem is not required for the initial product.

---

# 15. Player Projections

The application shall support statistical player projections obtained from one or more external fantasy-data providers and cached locally.

Projection fields may include:

### Quarterbacks

- Passing attempts.
- Completions.
- Passing yards.
- Passing touchdowns.
- Interceptions.

### Running Backs

- Rushing attempts.
- Rushing yards.
- Rushing touchdowns.
- Targets.
- Receptions.
- Receiving yards.
- Receiving touchdowns.

### Wide Receivers and Tight Ends

- Targets.
- Receptions.
- Receiving yards.
- Receiving touchdowns.
- Rushing statistics where appropriate.

The application shall use league scoring rules to calculate league-specific projected fantasy points from cached statistical projections.

The initial product does **not** require developing its own statistical projection model.

File-based projection import may be supported as a fallback or advanced feature but is not required to be the primary projection workflow.

---

# 16. Average Draft Position

The application shall support ADP data obtained from one or more external fantasy-data providers and cached locally.

ADP may be stored internally using the provider's original overall-pick format.

The preferred user-interface representation shall be **round.pick notation** calculated from the number of teams in the current league.

For a 12-team league:

```text
Overall 1     -> 1.01
Overall 12    -> 1.12
Overall 13    -> 2.01
Overall 25    -> 3.01
Overall 36    -> 3.12
Overall 37    -> 4.01
```

The same overall ADP shall automatically display differently for leagues of different sizes.

Example:

```text
Overall ADP 25

12 teams -> 3.01
10 teams -> 3.05
14 teams -> 2.11
```

For fractional ADP values, the original value shall be retained.

Example:

```text
Displayed ADP: 3.01
Raw ADP:       25.4
```

The user should be able to view both values when desired.

File-based ADP import may be supported as a fallback or advanced feature but is not required to be the primary ADP workflow.

## 16.1 External Fantasy-Data Provider Architecture

Player metadata, rankings, projections, ADP, tiers, and related draft information shall be obtained through interchangeable fantasy-data-provider adapters.

Conceptually:

```text
FantasyPros / Other Provider
            |
            v
   FantasyDataProvider
            |
            v
      Local SQLite Cache
            |
            v
 Draft Engine / Analytics / AI
```

Provider-specific APIs shall not be exposed directly to the draft engine or AI providers.

A provider adapter may supply some or all of:

- Player metadata.
- External player identifiers.
- Rankings.
- Position rankings.
- Tiers.
- ADP.
- Statistical projections.
- Player status or news metadata where useful.

No single external fantasy-data provider shall be required by the core draft engine.

## 16.2 Local Fantasy-Data Cache

Fantasy data needed during a draft shall be cached locally before draft time.

The application shall retain enough locally cached information to continue presenting useful draft information if the external fantasy-data provider becomes unavailable.

At minimum, the local draft-day cache should preserve the most recently retrieved:

- Player database.
- Rankings.
- ADP.
- Tiers where available.
- Projections where available.
- Player status with timestamp.
- Provider/source identity.
- Retrieval timestamp.

The Draft Room shall use the local cache as its normal read source during the draft rather than requiring a network request for every displayed player or AI decision.

Provider refreshes shall update the cache asynchronously and shall not block draft recording.

The UI shall expose data freshness so the user can determine when cached rankings, ADP, projections, or status information was last updated.

---

# 17. Draft Input Architecture

The draft engine shall not depend directly on the method used to obtain selections.

Draft input shall use interchangeable sources.

Initial sources shall include:

```text
YahooDraftSource
ManualDraftSource
```

Additional draft sources may be added later.

A successfully obtained draft selection shall be converted into a common internal draft event regardless of its source.

---

# 18. Yahoo Live Draft Source

For a draft conducted through Yahoo, the application shall normally detect selections made in the Yahoo draft automatically.

The Yahoo draft source shall:

- Detect newly drafted players.
- Determine the fantasy team receiving each player.
- Convert the Yahoo selection into the application's common draft-event format.
- Commit each accepted selection to local persistent storage.
- Update team rosters.
- Remove drafted players from availability.
- Advance the internal draft state.
- Detect discrepancies where practical.

The polling or synchronization strategy shall prioritize low latency while respecting Yahoo API limitations.

The application shall visibly indicate:

- Yahoo connection status.
- Current draft-input mode.
- Most recent successful synchronization.
- Last Yahoo pick observed.
- Whether local draft state matches the currently retrieved Yahoo state.
- Whether Yahoo-derived information may be stale.

AI analysis shall not block Yahoo draft synchronization.

Yahoo shall be treated as an input source, not as the owner of the application's draft state. Once a Yahoo pick has been accepted and committed, the durable local event history is the application state used by the UI, analytics, and AI layers.

## 18.1 Yahoo Failure and Manual Failover

Failure of Yahoo synchronization shall not stop the draft.

The manual draft source shall remain available at all times during a Yahoo-hosted draft, not only after a detected synchronization failure. A user watching the live draft in person or on a stream may legitimately enter a pick manually before Yahoo's own synchronization has caught up to it — for example, because the user saw the pick happen faster than Yahoo's polling interval reports it. This is a routine, supported use of manual entry alongside Yahoo Synchronized mode, not solely an emergency fallback, and it shall not require switching the application into any special or degraded state to do.

If Yahoo synchronization becomes unavailable or unreliable, the application shall allow the user to continue immediately using manual entry from the current local draft position.

Example:

```text
YAHOO SYNC LOST

Last Yahoo-synchronized pick: 5.08
Current local pick:           5.09

[ Retry Yahoo ]
[ Continue Manually ]
```

Switching to manual entry shall not:

- Create a new draft.
- Discard existing Yahoo-derived picks.
- Reset AI conversation context.
- Reset draft analytics.
- Require re-entering prior selections.

The draft shall continue using the same local event timeline.

Any pick entered manually while nominally in Yahoo Synchronized mode — whether prompted by a detected failure or entered proactively ahead of Yahoo's own update — shall be reconciled against Yahoo's data using the process in Section 18.2 once Yahoo reports that pick. Reconciliation is therefore a routine part of normal Yahoo-hosted drafting, not solely a post-outage recovery step.

## 18.2 Yahoo Reconnection and Reconciliation

If Yahoo connectivity returns after one or more picks were entered manually, the application shall compare the retrieved Yahoo draft history with the local active timeline.

Matching picks may be automatically reconciled.

Example:

```text
Local 5.09: Player A
Yahoo 5.09: Player A
Result: confirmed
```

A conflicting pick shall not be silently overwritten.

Example:

```text
SYNC CONFLICT

Pick 5.10

Local: Player B
Yahoo: Player X

[ Use Yahoo ]
[ Keep Local ]
[ Review Draft Board ]
```

Resolving a conflict shall use the same rollback-and-correction mechanism defined in Section 21 (Draft Rollback) and Section 23 (Corrections) rather than a separate state-mutation code path:

- Choosing **Use Yahoo** shall roll the active timeline back to the conflicting pick and commit the Yahoo-confirmed selection in its place, exactly as a manual correction would. Because rollback invalidates everything after the rollback point (Section 21), any local picks recorded after the conflict point shall be flagged for the user to review and, where they no longer reflect what actually happened, re-enter or re-reconcile as further Yahoo data arrives.
- Choosing **Keep Local** shall leave the active timeline unchanged. The Yahoo-reported value shall be recorded as a noted discrepancy for audit purposes rather than applied as a correction.

Reconciliation is a specific, guided trigger for the same rollback/correction primitives used elsewhere in the application; it is not an independent mechanism, so the two cannot drift apart in behavior or integrity guarantees over time.

The application shall preserve enough history to audit or reverse any reconciliation decision.

Yahoo reconnection shall never automatically discard manually committed draft history without explicit user action when a conflict exists.

## 18.3 Draft-Source Mode

The application shall clearly distinguish at least:

```text
Yahoo Synchronized
Manual
Yahoo Reconnecting
Reconciliation Required
```

Draft-source mode reflects overall synchronization health. It is distinct from the per-event `Source` field recorded on each draft event (Section 22): a single pick entered manually while the application is otherwise in Yahoo Synchronized mode does not, by itself, change the overall mode. The mode changes to Reconciliation Required only when a genuine conflict is detected per Section 18.2.

Draft-source mode is operational metadata and shall not change the common internal representation of a completed draft selection.

## 18.4 Draft-Day Readiness Check

Before a live draft begins, the application shall provide a readiness check.

The check should verify or display at least:

```text
League configuration           Ready / Attention
Draft order                    Ready / Attention
Keepers                        Ready / Attention
Local player database          Cached / Missing
Rankings                       Cached / Missing
ADP                            Cached / Missing
Projections                    Cached / Optional / Missing
Player status                  Cached, last updated <time>

Yahoo authentication           Connected / Failed
Yahoo league access            Connected / Failed
Yahoo draft access             Connected / Failed

OpenAI                         Connected / Disabled / Failed
Anthropic                      Connected / Disabled / Failed
xAI                            Connected / Disabled / Failed

Active draft database          Healthy
Latest backup                  <timestamp>
Manual fallback               Ready
```

A failed optional external provider shall not necessarily prevent beginning a draft if sufficient local data exists and manual draft entry is available.

The readiness screen shall make clear which failures are critical and which only reduce convenience or analytical quality.

---

# 19. Manual Draft Source

Manual draft entry shall be optimized for rapid use during an in-person draft and shall also serve as the immediate fallback input source for Yahoo-hosted drafts.

Because the application already knows which fantasy team is currently selecting, normal draft entry should require only selecting the drafted player.

Example:

```text
Round 4
Pick 4.07
Steve is selecting

Search: [ hall ]

Breece Hall       RB
Braelon Allen     RB
```

Selecting a player shall:

1. Validate the selection.
2. Persist the selection.
3. Assign the player to the current fantasy team.
4. Mark the player unavailable.
5. Update all derived draft state.
6. Advance to the next selection.

The UI shall not advance until the pick has been successfully committed to persistent storage.

Manual draft controls shall remain accessible even when the draft is currently Yahoo-synchronized so the user can continue the draft immediately if synchronization fails, or enter a pick proactively ahead of Yahoo's own update per Section 18.1.

---

# 20. Draft Queue

The application shall maintain a user-managed draft queue: an ordered list of players the user is considering for upcoming selections, independent of the underlying draft mechanics.

The queue shall:

- Be ordered by user preference, not necessarily by rank or ADP, though it may be seeded from a ranking source.
- Automatically remove a queued player once that player is drafted by any team.
- Automatically remove a queued player once used for the user's own selection.
- Remain visible in the Draft Room at all times (Section 58).
- Support quick reordering, by drag-and-drop and/or keyboard.
- Support adding players directly from the available-player list or search.

Example:

```text
My Queue

1. Puka Nacua        WR
2. Kyren Williams     RB
3. Sam LaPorta        TE
4. Josh Allen         QB
```

Manual draft entry shall support drafting the top remaining queued player with a single action (Section 60), without requiring the player to be re-located via search. This is the primary reason the queue exists: to remove searching from the critical path when the clock is running.

The queue shall be included in the AI-visible decision context (Section 45) so AI recommendations can reference and reason about players the user has already flagged as under consideration.

The queue is advisory only. It shall never cause a player to be drafted automatically without explicit user action.

---

# 21. Draft Rollback

The application shall support rolling the active draft back to **any previous selection**, including the beginning of the draft.

Rollback shall not be limited to the most recent pick.

Example:

```text
Current:
7.08 completed

Commissioner backs the real draft up to 7.02.

Application:
Rollback to 7.02

Result:
7.03 through 7.08 are no longer part of the active timeline.
Draft resumes at 7.03.
```

Rollback shall immediately recalculate:

- Available players.
- Fantasy rosters.
- Current selection.
- Upcoming selections.
- Positional counts.
- Remaining tiers.
- Position runs.
- ADP/value information.
- AI-visible state.
- Alerts.

Players removed by rollback shall become available again unless another condition such as keeper status makes them unavailable.

Rollback is the general mechanism used any time the active timeline must be rewound to a prior point, whether triggered directly by the user (this section) or indirectly through Yahoo reconciliation (Section 18.2).

## 21.1 Redo

Rollback shall support redo, restoring picks that were undone by a rollback provided no new pick has since been made that would conflict with them.

Example:

```text
Rollback to 7.02
(7.03 - 7.08 removed from active timeline)

Redo
(7.03 - 7.08 restored, provided nothing new was entered at 7.03+)
```

Once a new selection is entered at or after the rollback point, the previously rolled-back picks are superseded and redo shall no longer be available for that sequence. This mirrors standard undo/redo semantics: redo is only valid until a new action branches away from the undone history.

---

# 22. Draft Event History

Draft state shall be represented using a durable event history rather than exclusively through mutable current-state records.

Possible events include:

```text
DraftStarted
PlayerDrafted
PickCorrected
DraftRolledBack
DraftRedone
DraftBranchCreated
DraftCompleted
```

A player draft event shall include at least:

- Event ID.
- Draft ID.
- Branch ID.
- Sequence number.
- Overall pick.
- Round.
- Round pick.
- Fantasy team.
- Player.
- Source.
- Timestamp.

Sources may include:

```text
Manual
Yahoo
Keeper
Simulation
```

Historical draft events shall not normally be physically deleted.

The current draft state shall be reconstructable from persisted draft history.

---

# 23. Corrections

The user shall be able to correct an incorrectly entered draft selection.

Corrections shall preserve historical information sufficient to determine:

- What was originally entered.
- What replaced it.
- When the correction occurred.

A correction shall update all derived draft state immediately.

Yahoo-reconciliation conflicts resolved in favor of Yahoo (Section 18.2) are a specific, guided case of a correction and shall preserve the same history.

---

# 24. Draft Branching and Scenario Analysis

The application shall support branching from any prior draft position.

This functionality is intended for:

- Mock drafts.
- Practice drafts.
- Post-draft analysis.
- Alternate strategy evaluation.
- "What if I had taken Player B instead?" experimentation.

Example:

```text
Original:

1.06  Bijan Robinson
2.07  Drake London
3.06  Josh Allen
4.07  James Cook

Alternate branch:

1.06  Bijan Robinson
2.07  Drake London
3.06  Garrett Wilson
4.07  ...
```

The alternate branch shall inherit all selections before the branch point.

Selections after the branch point shall be independent.

The original draft shall remain intact.

---

# 25. Branch Identification

Draft branches shall have names.

Examples:

```text
Main Draft
RB/RB Experiment
WR at 3.06
Mock Draft #2
Post-Draft Alternative
```

The application shall clearly indicate which branch is currently active.

Switching branches shall restore the correct:

- Draft board.
- Available player pool.
- Team rosters.
- Current pick.
- Analytics.
- AI-visible context.

---

# 26. Scenario Comparison

The application should eventually support comparing alternate branches.

Useful comparison information may include:

- Final roster.
- Projected total fantasy points.
- Projected starting-lineup points.
- Positional strength.
- Value relative to ADP.
- Value relative to rankings.
- Bench quality.
- Position scarcity encountered.
- Players lost before subsequent selections.

---

# 27. Continuous Persistence

Draft state shall be continuously persisted.

There shall be no requirement to manually save a live draft.

A draft selection shall not be considered successfully entered until it has been committed to persistent local storage.

State changes requiring immediate persistence include:

- Draft selections.
- Rollbacks and redos.
- Corrections.
- Keeper modifications.
- Draft-order modifications.
- Branch creation.
- Active-branch changes.
- Other changes affecting the live draft.

Critical draft-state writes shall not rely on a periodic autosave interval.

---

# 28. Atomic Draft Operations

Draft operations shall be atomic.

A selection logically changes several pieces of state:

```text
Record selection
Assign player
Mark player unavailable
Update roster
Advance draft position
Update event history
```

These changes shall either all succeed or none succeed.

The application shall never intentionally leave states such as:

- Player unavailable without a corresponding active selection.
- Player present on two fantasy teams.
- Draft advanced without the previous selection being stored.
- Player on a roster while still appearing as available.

Database transactions shall be used for critical draft operations.

---

# 29. Crash Recovery

The application shall automatically recover an interrupted draft.

Possible failures include:

- Application crash.
- Forced process termination.
- Operating-system failure.
- Desktop-session failure.
- Unexpected restart.
- Power interruption after previously acknowledged selections have reached durable storage.

After restarting, the application shall:

1. Detect an unfinished draft.
2. Load the most recent committed draft state.
3. Restore the active branch.
4. Restore all completed picks.
5. Restore team rosters.
6. Restore player availability.
7. Restore the current draft position.
8. Recalculate deterministic analytics.
9. Resume AI context from the recovered state.

Expected behavior:

```text
Before crash:
7.08 completed

After restart:
7.08 remains completed
Next selection: 7.09
```

The design goal shall be:

> Any draft pick acknowledged by the application as successfully entered must be recoverable after restart.

---

# 30. Recovery Verification

When loading an interrupted draft, the application shall validate the reconstructed state.

Validation should include:

- No player actively drafted more than once.
- Every active selection belongs to a valid draft slot.
- Team rosters agree with active selections.
- Available-player status agrees with active selections and keepers.
- Current selection follows the most recent active pick.
- Keeper assignments remain valid.

Detected inconsistencies shall be reported clearly.

The application shall not silently invent or guess missing draft state.

---

# 31. Automatic Backups

The application should maintain automatic backups in addition to normal SQLite durability.

Suggested backup points include:

- Before a live draft begins.
- At the end of each round.
- Before a rollback.
- Before a new branch is created.
- Periodically during an active draft.

Multiple recent backup generations should be retained.

Backups shall not interfere with live draft entry.

---

# 32. Live Draft State

The application shall continuously know:

- Current round.
- Current overall selection.
- Current round.pick.
- Current fantasy team.
- User's next selection.
- Picks until user's next selection.
- Time remaining on the current pick, where available (Section 33).
- All previous active selections.
- All available players.
- Every fantasy team's current roster.
- Remaining roster requirements.
- Keeper selections.
- The active draft queue (Section 20).
- Recent draft activity.
- Current draft branch.
- Draft state version.

---

# 33. Draft Pick Clock

Where the pick source can supply it, the application shall track and display the actual time remaining on the current pick rather than only a count of picks until the user's next selection.

## 33.1 Yahoo Drafts

For Yahoo-hosted drafts, the pick clock shall be derived from Yahoo's own draft timer where the Yahoo API exposes it. The displayed countdown shall be kept in sync with Yahoo's synchronization cadence (Section 18) and shall visibly indicate if it may be stale due to a missed sync.

## 33.2 In-Person / Manual Drafts

For manual entry, a pick clock is optional and, if used, shall be a locally configured countdown (e.g., a commissioner-defined seconds-per-pick limit) rather than anything synchronized externally. The application shall not assume all in-person drafts use a timer.

## 33.3 Use in AI and Analysis

Remaining time on the current pick shall be available to Fast Draft Analysis Mode (Section 46) so that AI response urgency and depth can be tuned relative to how much time is actually left, not just picks-until-turn.

---

# 34. Draft State Versioning

Every committed state-changing draft operation shall advance a draft-state version number.

Example:

```text
DraftStateVersion = 47
```

AI requests shall record which draft-state version they analyzed.

If the draft changes while an AI response is being generated, the UI shall be capable of identifying that the response is based on an older draft state.

Example:

```text
AI analysis version: 47
Current draft version: 49
```

The application may:

- Mark the response as stale.
- Request an updated response.
- Continue displaying it with a visible state-version indication.

---

# 35. Deterministic Draft Analytics

Calculations that can reliably be performed locally shall not be delegated to AI.

The analytics engine should calculate information such as:

- Players drafted by position.
- Players drafted recently by position.
- Position runs.
- Available players by position.
- Remaining players by tier.
- Roster requirements for every fantasy team.
- Teams likely to need each position.
- Picks until user's next selection.
- ADP versus current pick.
- Ranking versus current pick.
- Projected league-specific points.
- Position scarcity, adjusted for Superflex/multi-QB QB demand where applicable (Section 8.1).
- Replacement-level values where implemented.
- Number of players remaining in relevant tiers.

AI models shall receive these calculated values rather than being required to repeatedly derive them from raw history.

---

# 36. Return-to-Next-Pick Analysis

The application should specifically support analysis of whether a player is likely to remain available until the user's next selection.

Relevant factors may include:

- Player ADP.
- Player ranking.
- Current overall pick.
- Number of intervening selections.
- Team rosters between the user's picks.
- Open starting positions on those teams.
- Position runs.
- Remaining positional tiers.
- Historical or source-specific draft tendencies where available.

This should allow analysis based on pick combinations rather than merely ranking the immediate choices.

Example:

```text
Option A

3.06 Garrett Wilson

Likely 4.07 RB options:
- Player A
- Player B
- Player C


Option B

3.06 James Cook

Likely 4.07 WR options:
- Player D
- Player E
- Player F
```

---

# 37. Draft Alerts

The application shall support proactive draft alerts.

Possible alerts include:

### Value Alert

```text
Fantasy ranking #23 remains available at pick 39.
```

### Position Alert

```text
Only one Tier 3 RB remains available.
```

### Draft Run

```text
Five WRs have been selected in the previous seven picks.
```

### Opponent Need

```text
Three of the four teams selecting before your next pick
still need a starting TE.
```

### QB Scarcity Alert (Superflex/Multi-QB leagues)

```text
Only three startable QBs remain, and two teams ahead of
your next pick still need a second QB.
```

Alerts shall be configurable to prevent excessive noise.

---

# 38. AI Provider Architecture

AI integration shall be provider-neutral.

Initial supported providers should include:

```text
OpenAI
Anthropic
xAI
```

Google (Gemini) is not part of the initial provider set, since it is not currently in active use, but the adapter architecture shall not preclude adding it or any other provider later without redesigning the draft engine.

Each provider shall be implemented behind an adapter.

Conceptually:

```text
                    Draft Query Service
                           |
          +----------------+----------------+
          |                |                |
      OpenAIAdapter   AnthropicAdapter   XaiAdapter
          |                |                |
       OpenAI            Claude            Grok
```

The core draft engine shall not depend on any specific AI provider.

Additional providers should be addable without redesigning the draft engine.

---

# 39. AI Provider Configuration

The application shall allow each AI provider to be:

- Enabled.
- Disabled.
- Independently configured.
- Assigned a model.
- Assigned an application role.

Possible roles include:

```text
Fast Advisor
Deep Advisor
Draft Watcher
Secondary Opinion
```

The same provider may potentially be used for more than one role with different model configurations.

Model identifiers shall be configurable and shall not be hard-coded into application logic.

---

# 40. AI API Credentials

The application shall support entering developer/API credentials for each supported provider.

Initial credentials include:

```text
OpenAI API Key
Anthropic API Key
xAI API Key
```

The credential UI shall accommodate additional providers, such as Google (Gemini), being configured later without requiring a redesign, even though no such provider is enabled by default.

Consumer subscriptions such as ChatGPT Plus, Claude Pro, or SuperGrok shall not be assumed to provide API access.

Each provider configuration screen shall clearly distinguish API access from consumer chat subscriptions.

---

# 41. Secure Credential Storage

AI API keys shall not be:

- Stored in plaintext in the SQLite fantasy database.
- Stored in exported league files.
- Included in backups of draft data.
- Embedded in distributed executables.
- Written to ordinary application logs.

The application should use operating-system-supported credential storage.

Examples include:

- Linux Secret Service-compatible credential storage where available.
- Windows Credential Manager or equivalent secure Windows facility.

If secure credential storage is unavailable, the application shall clearly notify the user before using any fallback mechanism.

Each person using a distributed copy of the application shall supply their own API credentials.

---

# 42. AI Provider Connection Testing

The configuration UI shall allow testing each provider.

Example:

```text
OpenAI
API Key: ***************
Model:   [selected-model]

[Test Connection]
```

The result shall clearly report:

- Authentication success.
- Authentication failure.
- Model unavailable.
- Network failure.
- Other provider error.

A failed AI provider shall not affect draft recording.

---

# 43. AI Draft Query Interface

AI models shall not access SQLite directly.

The application shall expose a controlled, primarily read-only query interface.

Representative functions include:

```text
GetLeagueSettings()
GetDraftStatus()
GetMyRoster()
GetTeamRoster(teamId)
GetAvailablePlayers(...)
GetPlayerDetails(...)
GetRecentPicks(...)
GetPositionSummary(...)
GetRemainingTiers(...)
GetUpcomingTeams(...)
GetDraftBoard(...)
GetMyQueue()
GetDecisionContext()
```

Provider adapters shall expose these operations through the tool/function-calling capabilities supported by each AI API.

---

# 44. Read-Only AI Rule

AI-facing tools shall be read-only for critical draft state in the initial implementation.

An AI model shall not directly:

- Draft a player.
- Roll back or redo the draft.
- Modify draft order.
- Change a keeper.
- Correct an existing selection.
- Switch the active draft branch.
- Add, remove, or reorder items in the draft queue.

The AI may recommend such actions.

Actual draft-state changes shall occur only through:

- The user interface.
- An authenticated draft input source such as Yahoo.

This prevents an incorrect AI response or hallucinated tool call from corrupting live draft state.

---

# 45. Decision Context

For latency-sensitive questions, the application shall be capable of constructing a compact precomputed decision context.

This context should contain enough information to answer common on-the-clock questions without requiring many sequential AI tool calls.

A decision context may include:

- Current pick.
- User's next pick.
- Picks until user's next pick.
- Time remaining on the current pick, where available.
- User roster.
- Open roster positions.
- User's draft queue.
- Top available players.
- Ranking.
- Position ranking.
- ADP.
- League-specific projection.
- Remaining tiers.
- Recent positional activity.
- Position needs of intervening fantasy teams.
- Draft-state version.

---

# 46. Fast Draft Analysis Mode

The application shall support a low-latency AI mode intended for timed online drafts.

This mode should:

- Use a precomputed decision context.
- Minimize sequential tool calls.
- Prefer concise responses.
- Use provider/model configurations selected for speed.
- Stream responses where supported.
- Take remaining pick-clock time into account (Section 33.3) when deciding how much analysis to request.

A typical response should quickly provide actionable information.

Example:

```text
1. Josh Allen
2. James Cook
3. Garrett Wilson

Recommendation: Allen.

QB1 offers substantially more separation from the remaining
QB tier than the available RB/WR options do from theirs.
```

---

# 47. Deep Analysis Mode

The application shall also support deeper AI analysis when latency is less important.

Deep analysis may allow broader tool use for questions such as:

```text
Compare Allen and Cook and analyze what the board is likely
to look like at my next pick.
```

This mode may use a slower or more capable model configuration.

---

# 48. Multiple AI Providers

Multiple enabled AI providers shall be capable of analyzing the same draft independently.

Each AI provider shall maintain its own conversation history.

Example:

```text
Grok
ChatGPT
Claude
```

All enabled providers shall receive access to the same underlying draft-state query service.

Their conclusions shall remain independent unless a future feature explicitly asks one model to critique another.

---

# 49. Parallel AI Requests

The application should support sending the same decision context to multiple AI providers simultaneously.

The UI shall display each response as it becomes available rather than waiting for all providers to finish.

Example:

```text
Grok       Ready
ChatGPT    Generating
Claude     Generating
```

This is especially important during online drafts where decision time is limited.

---

# 50. AI Response Streaming

Provider adapters should use streaming where supported.

The UI shall display useful response content as it arrives rather than waiting for the complete response.

Time to first useful information is considered important during a live draft.

---

# 51. AI Draft Watcher

The application should support an AI-assisted Draft Watcher.

The local analytics engine shall first determine whether a potentially significant event has occurred.

Examples:

- User's pick approaching.
- Major player falling far beyond ADP.
- Position tier nearly exhausted.
- Significant positional run.
- Unexpected roster behavior from nearby teams.

Only meaningful events should trigger AI analysis.

The Draft Watcher should normally produce very short output such as:

```text
RB ALERT

Only one Tier 2 RB remains, and two of the three teams
before your pick still need RB.
```

---

# 52. AI Usage and Cost Tracking

The application should record provider usage information returned by the AI APIs.

Where available, tracking should include:

- Provider.
- Model.
- Request count.
- Input tokens.
- Output tokens.
- Cached tokens where applicable.
- Estimated cost.
- Response latency.
- Time to first token where measurable.
- Total response time.

Example:

```text
AI Usage — Current Draft

Provider     Calls    Cost    Avg Response
Grok           63     $0.84      3.2 sec
OpenAI         18     $0.61      5.1 sec
Claude          9     $0.73      9.4 sec
```

The values shown above are illustrative only.

Tracking shall use actual provider usage information and configured pricing where available.

---

# 53. AI Spending Limits

The application shall support configurable AI spending limits so that live-draft AI usage, especially multiple parallel frontier-model calls (Section 49), cannot silently run up an unexpectedly large bill.

## 53.1 Configurable Limits

The user shall be able to configure, per provider and/or overall:

```text
Per-draft spending limit
Per-session (daily) spending limit
```

Limits shall be optional; a user who does not configure one is not blocked.

## 53.2 Warning and Enforcement

As estimated spend (Section 52) approaches a configured limit, the application shall surface a visible warning.

Once a configured limit is reached, the application shall stop issuing further AI requests to the affected provider(s) until the user raises the limit or a new draft/session begins.

## 53.3 Interaction with Draft Recording

Reaching an AI spending limit shall never affect draft recording or any other core function. This is a specific case of the general AI Failure Isolation rule (Section 54): the draft continues regardless of AI availability, whether that unavailability is caused by a provider outage or a spending limit.

---

# 54. AI Failure Isolation

Failure of an AI service shall never prevent draft recording.

Examples:

```text
OpenAI unavailable        -> Draft continues.
Claude unavailable        -> Draft continues.
xAI unavailable           -> Draft continues.
All AI providers down     -> Draft continues.
AI spending limit reached -> Draft continues.
```

AI functionality shall remain downstream from critical draft-state persistence.

---

# 55. Optional MCP Interface

The primary embedded AI integration shall initially use provider-native API/function calling.

The application architecture should permit an optional MCP server to expose the same read-only draft query service to external AI clients.

Conceptually:

```text
                    IDraftQueryService
                           |
       +-------------------+-------------------+
       |                   |                   |
   OpenAI Adapter     Anthropic Adapter     xAI Adapter
                           |
                     Optional MCP Server
```

MCP shall be considered an additional interface rather than a required foundation of the draft engine.

---

# 56. User Interface

The primary interface shall be a desktop application.

The visual design should be:

- Professional.
- Information-dense.
- Fast to navigate.
- Appropriate for a large desktop monitor.
- Consistent across Linux and Windows.
- Suitable for dark and light themes.

The application should not resemble a developer/debugging tool.

---

# 57. Primary Screens

Initial application screens should include:

1. League Selection.
2. League Setup.
3. Yahoo Import Review.
4. Draft Order Editor.
5. Keeper Editor.
6. Player Data Sources and Cache Status.
7. AI Provider Configuration (including spending limits).
8. Draft Readiness.
9. Draft Room.
10. Draft History.
11. Scenario/Branch Selection.
12. Post-Draft Grade and Recap.

---

# 58. Draft Room

The Draft Room is the application's primary interface.

It shall prioritize:

- Current round.
- Current pick.
- Team currently selecting.
- User's upcoming pick.
- Time remaining on the current pick, where available.
- Available-player list.
- Draft board.
- My Queue.
- User roster.
- Significant alerts.
- AI analysis.

A conceptual layout:

```text
+------------------------------------------------------------------------+
| League | Round 5 | Pick 5.03 | 0:47 | Mike selecting | Kelly in 3 picks|
+----------------------+-----------------------------+-------------------+
|                      |                             |                   |
| Draft Board          | Available Players           | AI Analysts       |
|                      |                             |                   |
| 4.10 Player          | Rank Player Pos ADP Proj    | Grok              |
| 4.11 Player          | 22   Smith WR 5.04 278      | ChatGPT           |
| 4.12 Player          | 25   Jones RB 5.08 251      | Claude            |
| 5.01 Player          |                             |                   |
| 5.02 Player          | Search [____________]       | Analysis...       |
+----------------------+-----------------------------+-------------------+
| My Queue: 1. Nacua  2. K.Williams  3. LaPorta  4. Allen                 |
+----------------------+-----------------------------+-------------------+
| ALERT: Only one Tier 3 RB remains                                      |
+------------------------------------------------------------------------+
| My Roster: QB - | RB Player | RB - | WR Player | WR Player | TE - ...  |
+------------------------------------------------------------------------+
```

The exact layout shall be refined during prototyping.

---

# 59. Available Player Display

The available-player list should support columns such as:

- Overall rank.
- Position rank.
- Player name.
- NFL team.
- Position.
- Tier.
- ADP in round.pick format.
- Raw ADP.
- Projected fantasy points.
- Bye week.
- Status (Section 13), shown as a compact indicator with detail on demand.

The list should support:

- Searching.
- Sorting.
- Position filtering.
- Tier filtering.
- Drafting by keyboard or mouse.
- Adding a player to the draft queue.
- Quick display of additional player details.

---

# 60. Manual Draft Keyboard Operation

The manual draft workflow should require minimal mouse interaction.

Desired keyboard operations include:

```text
/            Focus player search
Enter        Draft selected player
Esc          Clear search
Ctrl+D       Draft the top player in My Queue
Ctrl+Z       Undo / begin rollback workflow
Ctrl+Shift+Z Redo (also Ctrl+Y)
```

Additional shortcuts shall be determined during UI testing.

Any shortcut that modifies critical draft state shall use appropriate confirmation or recovery behavior.

---

# 61. AI User Interface

The Draft Room shall support multiple AI analysts.

Possible UI arrangements include:

- Tabs.
- Split panes.
- Selectable primary analyst.
- Collapsible analyst panel.

Each AI panel should display:

- Provider.
- Model.
- Current status.
- Response.
- Draft-state version analyzed.
- Response latency where useful.

The AI interface shall not obscure core draft-entry controls.

---

# 62. Data Storage

SQLite shall be the primary application data store.

Conceptual database entities include:

```text
Leagues
Teams
RosterSlots
ScoringRules

Drafts
DraftBranches
DraftSlots
DraftEvents
DraftCheckpoints
Keepers
DraftQueueItems

Players
PlayerProviderIds
PlayerStatus

FantasyDataProviders
FantasyDataRefreshes

RankingSources
PlayerRankings

ProjectionSources
PlayerProjections

AdpSources
PlayerAdp

AiUsageRecords
```

Provider-fetched fantasy information shall be persisted locally so the Draft Room, analytics engine, and AI decision context can operate against cached data during a network outage.

JSON and CSV may be used for:

- Import/export.
- Debugging.
- Portable configuration.
- Backups where useful.
- Optional manual fallback data import.

They shall not be the primary mutable store for live draft state.

---

# 63. SQLite Durability

SQLite configuration shall prioritize draft-state durability.

Write-Ahead Logging should be evaluated for use.

All critical state transitions shall use transactions.

Derived data may be cached for performance but shall be reproducible from persisted draft information.

---

# 64. Reliability Priority

Reliability of draft recording is more important than:

- Yahoo synchronization.
- Fantasy-data-provider availability.
- AI availability.
- AI response speed.
- Analytics availability.
- Ranking updates.
- Projection updates.
- Cosmetic UI behavior.

The critical path shall be:

```text
Draft input
    |
Validation
    |
Persistent commit
    |
Draft state updated
    |
UI updated
    |
Analytics / AI notified
```

AI and external services shall not sit in the critical persistence path.

A network service may enhance the draft, but no network service shall be required to continue recording and managing the draft.

If external integrations fail simultaneously, the application should remain capable of:

- Recording picks manually.
- Displaying the local draft board.
- Displaying team rosters.
- Displaying available players.
- Using locally cached rankings.
- Using locally cached ADP.
- Using locally cached projections where available.
- Calculating deterministic roster and draft analytics.
- Using the draft queue.
- Rolling back and redoing picks.
- Creating and restoring branches.
- Persisting and recovering the draft.

Loss of Yahoo synchronization may require manual pick entry.

Loss of fantasy-data providers may prevent refreshing data but shall not remove already cached information.

Loss of AI providers shall remove AI advice but shall not affect the draft engine.

---

# 65. Draft Integrity Rules

The application shall enforce core integrity rules including:

- A player cannot be drafted twice on the same active branch.
- A draft slot cannot contain multiple active selections.
- A team cannot receive a player from an invalid draft slot.
- Keeper players cannot be drafted normally.
- A fantasy team cannot have more than one keeper.
- Rollback must restore player availability correctly.
- Redo must not restore a pick that conflicts with a newer selection.
- Branches must remain independent after their branch point.
- Current draft position must agree with the active timeline.
- Yahoo reconciliation must resolve conflicts through rollback/correction (Sections 18.2, 21, 23) rather than a separate mutation path.

---

# 66. Post-Draft Grade and Recap

Once a draft is complete, the application shall generate a Post-Draft Grade and Recap combining deterministic analytics with AI-generated commentary.

## 66.1 Deterministic Inputs

The recap shall reuse the existing analytics engine (Section 35) rather than recomputing separately, drawing on values such as:

- Final roster by position.
- Projected starting-lineup points.
- Positional strength relative to the rest of the league.
- Value relative to ADP and rankings across all selections.
- Bench quality and depth.
- Roster requirements filled versus any left thin.

## 66.2 AI-Generated Narrative

Each enabled AI provider shall be able to independently generate a qualitative grade and narrative summary of the draft, using the same read-only query service (Section 43) used during the live draft plus the deterministic recap inputs above.

Providers' recaps shall remain independent, consistent with the general rule that multiple AI providers do not merge conclusions unless explicitly asked to (Section 48).

## 66.3 Availability

The recap shall be generated after the draft is marked complete and shall also be viewable later from Draft History (Section 57) without requiring a live draft session.

The recap shall be available even if AI providers are unavailable at generation time, using deterministic analytics alone in that case; AI narrative may be requested again later once providers are reachable.

---

# 67. Initial Development Milestone

The first useful development milestone should not require a live Yahoo draft.

Initial functionality should include:

1. Application shell.
2. SQLite schema.
3. Local player/fantasy-data cache model.
4. Fantasy-data-provider abstraction.
5. Manual league creation.
6. Roster configuration, including Superflex/multi-QB.
7. Scoring configuration.
8. Draft-order editor.
9. Keeper editor (single keeper per team).
10. Round.pick ADP conversion.
11. Manual draft engine.
12. Draft board.
13. Available-player list.
14. Draft queue.
15. Team roster tracking.
16. Continuous persistence.
17. Crash recovery.
18. Rollback and redo.
19. Basic branching.
20. Deterministic analytics using locally cached data.
21. One AI provider.
22. Decision-context generation.
23. Provider/cache freshness display.

The initial milestone may use a simple supported data source or seed dataset to populate the cache. It does not require a generalized ranking/projection/ADP file-import framework.

This allows the entire draft-engine, persistence, analytics, UI, and AI architecture to be exercised using mock drafts before depending on Yahoo live-draft integration.

---

# 68. Later Development Milestones

Later milestones may add:

### Yahoo Integration

- OAuth setup.
- League import.
- Live Yahoo draft monitoring, including pick-clock synchronization where available.
- Immediate Yahoo-to-manual failover.
- Yahoo reconnection and state reconciliation.
- Draft-day Yahoo readiness checks.

### Fantasy Data Providers

- FantasyPros or another primary fantasy-data-provider adapter.
- Additional ranking sources.
- Additional projection sources.
- Multiple ADP sources.
- Tiers.
- Dedicated player status/injury-news source if needed.
- Optional file-based import/export fallbacks.

### Additional AI Providers

- OpenAI.
- Anthropic.
- xAI.
- Google (Gemini), if it becomes worth adding later.
- Additional future providers.

### Advanced Analytics

- Position scarcity.
- Value-over-replacement analysis.
- Return-to-next-pick probability.
- Team-needs prediction.
- Draft-pattern analysis.
- Scenario comparison.

### External Integration

- Optional MCP server.
- Export of completed drafts.

---

# 69. Success Criteria

The application shall be considered successful when an entire live fantasy draft can be conducted while the program continuously knows:

- Every active draft selection.
- Every available player.
- Every fantasy team's roster.
- Every keeper.
- Every upcoming draft selection.
- The user's current roster requirements.
- The league's exact scoring rules.
- Locally cached player rankings.
- Locally cached player ADP.
- Locally cached player projections when available.
- Significant positional trends.
- Relevant draft value changes.
- Which draft input source is currently active and whether synchronization requires attention.

During an in-person draft, each selection should normally need to be entered only once.

During a healthy Yahoo-hosted draft, selections should normally require no manual entry.

If Yahoo synchronization fails during a Yahoo-hosted draft, the user shall be able to continue immediately using manual entry without reconstructing previous picks or creating a new draft session.

If Yahoo later reconnects, matching picks shall be reconciled and conflicting picks shall be surfaced for explicit resolution using the same rollback/correction mechanism as any other pick fix.

At any point, a configured AI assistant should be able to answer a strategy question using the current live draft state without the user manually explaining what has happened.

The application shall remain useful with cached player data if a fantasy-data provider cannot be reached during the draft.

The application shall survive an unexpected restart without requiring reconstruction of previously committed draft selections.

The user shall be able to roll a draft back to any previous pick and redo that rollback.

The user shall be able to create alternate draft branches from previous states for practice and scenario analysis.

The draft shall remain fully usable for recording, board display, roster tracking, rollback, and deterministic analytics if every configured AI provider becomes unavailable or a spending limit is reached.

A useful post-draft grade and recap shall be available immediately after the draft completes.

---

# 70. Open Design Questions

The following items do not prevent initial development but require later decisions or experimentation.

## 70.1 Fantasy Data Providers

Determine the preferred provider or providers for:

- Player metadata.
- Rankings.
- Projections.
- ADP.
- Tiers.
- Player status/injury news beyond what Yahoo provides.

For each candidate provider, determine:

- Whether a documented API is available.
- Authentication requirements.
- Licensing or subscription requirements.
- Rate limits.
- Data freshness.
- Player identifier quality.
- Which data can be legally and reliably cached locally.
- Failure behavior.

The design shall allow providers to change without modifying the core draft engine.

## 70.2 Yahoo Live Draft Synchronization

Determine:

- Practical polling frequency.
- API limitations.
- How quickly completed Yahoo picks become visible.
- Whether Yahoo's API exposes a live pick-clock value, and how reliably.
- Recovery behavior after temporary Yahoo connectivity loss.
- Exact reconciliation rules when local and Yahoo state differ.
- Whether Yahoo exposes enough historical live-draft state to validate manual picks entered during an outage.
- How API changes or authorization failures are detected before draft time.

## 70.3 AI Model Selection

Benchmark candidate models for:

- Response quality.
- Time to first token.
- Total response time.
- Tool-call performance.
- Cost.

No provider shall be assumed to be permanently fastest or best.

## 70.4 Draft Watcher Thresholds

Determine which events justify interrupting the user during a live draft.

The watcher must provide value without producing excessive alerts.

## 70.5 Return-to-Next-Pick Prediction

Determine whether this should initially use:

- ADP heuristics.
- Deterministic probability models.
- AI reasoning.
- Historical draft data.
- A combination of these approaches.

## 70.6 UI Layout

Prototype the Draft Room before finalizing implementation.

Important questions include:

- How much space the full draft board receives.
- Whether all AI analysts remain visible simultaneously.
- How the user's roster is displayed.
- How alerts are surfaced.
- How available-player information is prioritized.
- How manual draft entry works under time pressure.
- Where the queue and pick clock sit relative to everything else.
- How Yahoo/manual mode and sync health are displayed without clutter.
- How sync conflicts are resolved quickly during a live draft.

## 70.7 AI Spending Limit Defaults

Determine sensible default per-draft and per-session spending limits, if any should be pre-populated, given the intent to use frontier-tier models rather than lower-cost models.

## 70.8 Draft-Day Cache Requirements

Determine the minimum cached dataset required to declare the application ready for a draft.

This should include a policy for:

- Maximum acceptable age of rankings.
- Maximum acceptable age of ADP.
- Maximum acceptable age of projections.
- Maximum acceptable age of player status.
- Behavior when one optional dataset is missing.
- Whether the user can explicitly acknowledge stale data and continue.

---

# 71. Design Principles

## Draft State Is Critical

Accurate draft state is the core function of the application.

Everything else depends on it.

## Persist Before Advancing

A pick is not complete until it has been committed to durable storage.

## Preserve History

Rollback and corrections shall not unnecessarily destroy historical information.

## Make Experimentation Cheap

Mock drafts and alternate scenarios should use the same draft engine as real drafts.

## Calculate Before Asking AI

Bookkeeping, arithmetic, roster validation, positional counts, and similar deterministic tasks belong in application code.

AI is primarily for:

- Interpretation.
- Strategy.
- Comparison.
- Prediction.
- Identifying unusual situations.

## External Services Are Enhancements, Not Dependencies

Yahoo, fantasy-data providers, and AI providers shall be integrated through replaceable adapters.

The application shall preserve enough local state and cached data to continue the draft when an external provider becomes unavailable.

## Keep AI Replaceable

OpenAI, Anthropic, xAI, or another future provider shall be adapters around a common application interface.

## Keep AI Read-Only

AI recommendations shall not directly modify critical draft state.

## Optimize for the Clock

During timed drafts, latency matters.

The application shall use techniques such as:

- Precomputed decision context.
- Streaming.
- Parallel AI requests.
- Fast-model configurations.
- Local deterministic analytics.
- Actual pick-clock awareness, not just picks-until-turn.

## Respect the Budget

Frontier-model AI usage is not free, especially with multiple providers running in parallel across a full draft. Spending limits and cost visibility are first-class, not an afterthought.

## One Correction Mechanism, Not Two

Any time a past pick must be rewritten — a manual mistake, a rollback, or a Yahoo reconciliation conflict — it goes through the same rollback/correction primitives (Sections 21, 23). Parallel, special-case ways of rewriting draft history are not introduced elsewhere in the application.

## Manual Correction Must Be Easy

Draft order, keepers, selections, and other human-managed information must be quickly correctable.

## The Draft Room Is the Product

Configuration screens are necessary, but the Draft Room shall receive the greatest usability and visual-design attention.

## Professional Without Being Sparse

The application should provide substantial useful information at a glance without looking like either a spreadsheet or a debugging console.
