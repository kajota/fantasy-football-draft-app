# Fantasy Draft Assistant
## Software Design Document

**Version:** 0.1  
**Date:** 2026-08-13  
**Status:** Initial design draft based on Software Requirements Specification v0.4

---

## 1. Purpose

This document translates the Fantasy Draft Assistant Software Requirements Specification v0.4 into an implementation-oriented software design.

The design emphasizes:

- Reliable local draft recording.
- Event-based draft history with rollback, redo, and branching.
- Replaceable external-provider adapters.
- Cached fantasy football data for draft-day resilience.
- Low-latency AI recommendations through read-only query services.
- A polished desktop UI built with C#, .NET, Avalonia UI, MVVM, and SQLite.

The requirements document remains the product source of truth. This design document describes one practical architecture for satisfying it.

---

## 2. Design Goals

1. Keep the draft engine independent from Yahoo, AI providers, and fantasy-data providers.
2. Persist every acknowledged draft change before the UI advances.
3. Make draft state reconstructable from durable local history.
4. Support manual draft entry as a first-class source at all times.
5. Treat Yahoo synchronization as an input stream, not as authoritative application state.
6. Use local cached player, ranking, projection, ADP, tier, and status data during drafts.
7. Expose AI access through controlled read-only application services.
8. Keep provider-specific code behind adapters.
9. Make the first development milestone useful without Yahoo live-draft integration.
10. Preserve clear paths for later Yahoo, additional AI, MCP, and advanced analytics work.

---

## 3. Technology Choices

| Concern | Design Choice |
| --- | --- |
| Language | C# |
| Runtime | .NET |
| Desktop UI | Avalonia UI |
| UI pattern | MVVM |
| Local database | SQLite |
| Persistence access | Repository/unit-of-work services over SQLite |
| Background work | Hosted application services or app-level schedulers |
| External HTTP | .NET HTTP APIs |
| Credentials | OS credential store abstraction |
| Tests | xUnit or NUnit, with core domain tests prioritized |

SQLite should be configured for durability. WAL mode should be evaluated early, but critical correctness comes from explicit transactions around draft operations.

---

## 4. Solution Structure

The solution should be split by responsibility rather than by provider.

```text
src/
  FantasyDraftAssistant.App/
    Avalonia application, views, view models, navigation, UI composition

  FantasyDraftAssistant.Core/
    Domain models, draft engine, roster/scoring rules, analytics contracts,
    provider-neutral interfaces, validation rules

  FantasyDraftAssistant.Data/
    SQLite schema, migrations, repositories, transaction/unit-of-work support,
    projections for fast UI reads

  FantasyDraftAssistant.Providers.Yahoo/
    Yahoo auth, league import, live draft polling/sync adapter

  FantasyDraftAssistant.Providers.FantasyData/
    Fantasy-data provider abstractions and initial seed/simple provider

  FantasyDraftAssistant.Providers.AI/
    AI provider contracts, OpenAI adapter first, later Anthropic/xAI adapters

  FantasyDraftAssistant.Mcp/
    Optional future MCP server exposing the read-only draft query service

tests/
  FantasyDraftAssistant.Core.Tests/
  FantasyDraftAssistant.Data.Tests/
  FantasyDraftAssistant.App.Tests/
```

Provider-specific projects can be added incrementally. The first milestone can ship with `Core`, `Data`, `App`, a simple fantasy-data provider, and one AI adapter.

---

## 5. Architectural Layers

```text
Avalonia UI / ViewModels
        |
Application Services
        |
Core Domain / Draft Engine / Analytics
        |
Persistence Interfaces
        |
SQLite Repositories

External Providers:
Yahoo, fantasy data, AI, optional MCP
connect through adapters into application/core interfaces.
```

### 5.1 UI Layer

Responsibilities:

- Render league setup, draft room, draft history, and configuration screens.
- Capture user commands.
- Display current draft state, data freshness, sync health, alerts, and AI results.
- Keep critical draft-entry controls responsive.

The UI must not mutate draft state directly. It sends commands to application services.

### 5.2 Application Services

Responsibilities:

- Coordinate use cases such as creating a league, importing settings, drafting a player, rollback, redo, branching, and generating AI decision context.
- Own transaction boundaries for critical operations.
- Call repositories, draft engine, analytics engine, and provider adapters in the correct order.
- Publish state-change notifications to the UI and background services.

### 5.3 Core Domain

Responsibilities:

- Represent league, roster, scoring, player, draft, branch, slot, event, keeper, and queue concepts.
- Validate draft operations.
- Apply draft events to reconstruct active state.
- Calculate deterministic analytics.
- Provide provider-neutral interfaces.

Core must not depend on Avalonia, SQLite, Yahoo SDKs, AI SDKs, or provider response types.

### 5.4 Infrastructure

Responsibilities:

- SQLite persistence.
- Schema migrations.
- Secure credential storage.
- HTTP clients.
- Provider-specific data mapping.
- Logging and diagnostics.

---

## 6. Core Domain Model

### 6.1 League

Represents a fantasy league configuration.

Key fields:

- `LeagueId`
- `Name`
- `Platform`
- `ExternalLeagueId`
- `Season`
- `TeamCount`
- `UserTeamId`
- `DraftType`
- `RoundCount`
- `RosterSize`
- `DraftSourcePreference`

### 6.2 Team

Represents a fantasy team in a league.

Key fields:

- `TeamId`
- `LeagueId`
- `Name`
- `OwnerName`
- `DisplayLabel`
- `DraftPosition`
- `ExternalTeamId`

### 6.3 Roster Slot

Represents required, flexible, bench, and inactive roster positions.

Key fields:

- `RosterSlotId`
- `LeagueId`
- `SlotCode`
- `SlotKind`
- `Count`
- `EligiblePositions`

Superflex is modeled as a flex slot whose `EligiblePositions` includes `QB`. No special draft-engine branch is required for Superflex.

### 6.4 Scoring Rule

Represents a league-specific scoring value.

Key fields:

- `ScoringRuleId`
- `LeagueId`
- `Category`
- `Points`

Projection scoring uses these actual point values rather than a generic scoring label.

### 6.5 Player

Represents an internal application player identity.

Key fields:

- `PlayerId`
- `Name`
- `NflTeam`
- `PrimaryPosition`
- `EligiblePositions`
- `ByeWeek`
- `Status`
- `StatusUpdatedAt`

Provider identifiers are stored separately so draft history always references the internal player ID.

### 6.6 Draft

Represents one draft session for one league.

Key fields:

- `DraftId`
- `LeagueId`
- `Name`
- `Season`
- `Status`
- `ActiveBranchId`
- `CurrentStateVersion`
- `CreatedAt`
- `StartedAt`
- `CompletedAt`

### 6.7 Draft Branch

Represents an active or alternate draft timeline.

Key fields:

- `BranchId`
- `DraftId`
- `Name`
- `ParentBranchId`
- `BranchPointOverallPick`
- `CreatedFromStateVersion`
- `CurrentHeadEventId`
- `CreatedAt`

The branch stores the current head pointer for fast recovery. The event log remains the audit trail.

### 6.8 Draft Slot

Represents a concrete pick in the draft order.

Key fields:

- `DraftSlotId`
- `DraftId`
- `OverallPick`
- `Round`
- `RoundPick`
- `TeamId`
- `IsKeeperSlot`

Draft slots are explicitly stored to support custom draft orders, keepers, and future nonstandard rules.

### 6.9 Keeper

Represents a single keeper assigned to a team.

Key fields:

- `KeeperId`
- `DraftId`
- `TeamId`
- `PlayerId`
- `RoundCost`
- `DraftSlotId`
- `Notes`

Validation enforces at most one keeper per team.

### 6.10 Draft Queue Item

Represents the user's ordered queue.

Key fields:

- `QueueItemId`
- `DraftId`
- `BranchId`
- `PlayerId`
- `SortOrder`
- `CreatedAt`

The queue is advisory only and never drafts automatically.

---

## 7. Draft Event Design

Draft history is durable and append-oriented. Current state is reconstructed by applying active events for a branch.

### 7.1 Event Types

Initial event types:

- `DraftStarted`
- `PlayerDrafted`
- `PickCorrected`
- `DraftRolledBack`
- `DraftRedone`
- `DraftBranchCreated`
- `DraftCompleted`

### 7.2 Event Record

Common fields:

- `EventId`
- `DraftId`
- `BranchId`
- `EventType`
- `StateVersion`
- `SequenceNumber`
- `CreatedAt`
- `CreatedBy`
- `CorrelationId`
- `PayloadJson`

Player draft payload:

- `DraftSlotId`
- `OverallPick`
- `Round`
- `RoundPick`
- `TeamId`
- `PlayerId`
- `Source`
- `ExternalSourceId`
- `ObservedAt`

### 7.3 Active Timeline

The active timeline for a branch is the ordered set of draft selections currently considered live for that branch.

Rollback and correction events do not physically delete prior events. Instead, they change which previously recorded selections are active in the branch projection.

For efficient reads, the database may maintain projection tables such as:

- `ActiveDraftSelections`
- `TeamRosterProjection`
- `PlayerAvailabilityProjection`
- `BranchHeadProjection`

These are derived from events and can be rebuilt.

### 7.4 State Versioning

Every committed state-changing operation increments `Draft.CurrentStateVersion`.

AI requests store:

- `DraftId`
- `BranchId`
- `AnalyzedStateVersion`
- `Provider`
- `Model`
- `RequestStartedAt`
- `ResponseCompletedAt`

If the current state version advances while an AI response is in flight, the UI marks the response stale.

---

## 8. Draft Operation Transaction Pattern

Every critical draft operation follows the same pattern:

```text
Begin SQLite transaction
  Load draft, branch, slot, player, and current projections
  Validate command
  Append event
  Update branch head/state version
  Update derived projections
Commit transaction
Publish state-change notification
Run analytics/AI updates asynchronously
```

The UI advances only after the transaction commits.

This pattern applies to:

- Manual selections.
- Yahoo selections.
- Keeper slot commits.
- Corrections.
- Rollback.
- Redo.
- Branch creation.
- Active branch changes.

---

## 9. Draft Engine Services

### 9.1 `IDraftCommandService`

Handles state-changing draft commands.

Representative methods:

```csharp
Task<DraftCommitResult> StartDraftAsync(StartDraftCommand command);
Task<DraftCommitResult> DraftPlayerAsync(DraftPlayerCommand command);
Task<DraftCommitResult> RollbackAsync(RollbackDraftCommand command);
Task<DraftCommitResult> RedoAsync(RedoDraftCommand command);
Task<DraftCommitResult> CorrectPickAsync(CorrectPickCommand command);
Task<DraftCommitResult> CreateBranchAsync(CreateDraftBranchCommand command);
Task<DraftCommitResult> SwitchBranchAsync(SwitchDraftBranchCommand command);
Task<DraftCommitResult> CompleteDraftAsync(CompleteDraftCommand command);
```

### 9.2 `IDraftStateService`

Provides read models for UI and analytics.

Representative methods:

```csharp
Task<DraftState> GetCurrentDraftStateAsync(DraftId draftId);
Task<DraftBoard> GetDraftBoardAsync(DraftId draftId, BranchId branchId);
Task<IReadOnlyList<PlayerSummary>> GetAvailablePlayersAsync(PlayerQuery query);
Task<TeamRoster> GetTeamRosterAsync(TeamId teamId, BranchId branchId);
Task<MyQueue> GetQueueAsync(DraftId draftId, BranchId branchId);
```

### 9.3 `IDraftValidationService`

Validates invariants:

- Player is available.
- Slot exists and belongs to the selecting team.
- Slot has no active selection.
- Keeper constraints are satisfied.
- Rollback target is valid.
- Redo does not conflict with newer events.
- Branch point exists.

### 9.4 `IAnalyticsService`

Computes deterministic analytics:

- Position counts.
- Recent position runs.
- Remaining tiers.
- Team needs.
- Picks until user's next pick.
- ADP and ranking value.
- Scored projections.
- Superflex-aware QB scarcity.
- Alerts.

---

## 10. Persistence Design

### 10.1 Primary Tables

Initial SQLite schema should include:

```text
Leagues
Teams
RosterSlots
RosterSlotEligiblePositions
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
PlayerStatuses

FantasyDataProviders
FantasyDataRefreshes
RankingSources
PlayerRankings
ProjectionSources
PlayerProjections
AdpSources
PlayerAdp
PlayerTiers

AiProviderConfigs
AiUsageRecords
AiResponses

ActiveDraftSelections
TeamRosterProjection
PlayerAvailabilityProjection
```

Projection tables are derived and may be rebuilt. Event and configuration tables are durable source data.

### 10.2 Transaction Boundaries

Transactions are required for:

- Pick commit.
- Rollback.
- Redo.
- Correction.
- Keeper changes.
- Draft-order changes.
- Branch creation.
- Active branch switch.
- Draft completion.

Provider cache refreshes use their own transactions and must not block live draft entry.

### 10.3 Backups

Backups are file-level or database-level copies taken:

- Before a live draft starts.
- At round boundaries.
- Before rollback.
- Before branch creation.
- Periodically during an active draft.

Backups run outside the critical pick-entry transaction.

---

## 11. Provider Adapter Design

### 11.1 Draft Input Sources

Common interface:

```csharp
public interface IDraftSource
{
    DraftSourceKind Kind { get; }
    IAsyncEnumerable<DraftSourceEvent> WatchAsync(DraftSourceContext context, CancellationToken cancellationToken);
}
```

Initial implementations:

- `ManualDraftSource`
- `YahooDraftSource`

Manual entry is always available. A manual event recorded while the overall mode is `YahooSynchronized` remains a normal `PlayerDrafted` event with source `Manual`.

### 11.2 Yahoo Integration

Yahoo adapter responsibilities:

- OAuth authentication.
- League import.
- Draft order import where available.
- Keeper import where available.
- Live draft polling.
- Pick-clock extraction where available.
- Sync health reporting.
- Reconciliation comparison.

Yahoo data is mapped into internal commands and models before reaching the draft engine.

### 11.3 Fantasy Data Providers

Common interface:

```csharp
public interface IFantasyDataProvider
{
    string ProviderKey { get; }
    Task<FantasyDataRefreshResult> RefreshAsync(FantasyDataRefreshRequest request, CancellationToken cancellationToken);
}
```

Provider refresh output is persisted into the local cache. The draft room reads from SQLite cache, not directly from provider APIs.

### 11.4 AI Providers

Common interface:

```csharp
public interface IAiProviderAdapter
{
    string ProviderKey { get; }
    Task<AiConnectionTestResult> TestConnectionAsync(AiProviderConfig config, CancellationToken cancellationToken);
    IAsyncEnumerable<AiResponseChunk> StreamAnalysisAsync(AiAnalysisRequest request, CancellationToken cancellationToken);
}
```

AI adapters receive structured decision context and read-only tool definitions. They do not receive write tools for draft state.

---

## 12. AI Query Service

AI providers access draft information through `IDraftQueryService`, not SQLite.

Representative methods:

```csharp
Task<LeagueSettingsDto> GetLeagueSettingsAsync(QueryContext context);
Task<DraftStatusDto> GetDraftStatusAsync(QueryContext context);
Task<RosterDto> GetMyRosterAsync(QueryContext context);
Task<RosterDto> GetTeamRosterAsync(QueryContext context, TeamId teamId);
Task<PlayerListDto> GetAvailablePlayersAsync(QueryContext context, PlayerFilter filter);
Task<PlayerDetailsDto> GetPlayerDetailsAsync(QueryContext context, PlayerId playerId);
Task<RecentPicksDto> GetRecentPicksAsync(QueryContext context, int count);
Task<PositionSummaryDto> GetPositionSummaryAsync(QueryContext context);
Task<RemainingTiersDto> GetRemainingTiersAsync(QueryContext context);
Task<UpcomingTeamsDto> GetUpcomingTeamsAsync(QueryContext context);
Task<DraftBoardDto> GetDraftBoardAsync(QueryContext context);
Task<MyQueueDto> GetMyQueueAsync(QueryContext context);
Task<DecisionContextDto> GetDecisionContextAsync(QueryContext context);
```

The service returns compact DTOs. It can enforce size limits and omit unnecessary raw history during fast draft mode.

---

## 13. Major Workflows

### 13.1 Manual Draft Pick

```text
User selects player
  |
ViewModel sends DraftPlayerCommand
  |
DraftCommandService opens transaction
  |
Validate slot, team, player availability, keeper status
  |
Append PlayerDrafted event
  |
Update active-selection, roster, availability, queue projections
  |
Increment state version
  |
Commit
  |
UI refreshes current pick, board, queue, roster, analytics
  |
AI responses for older versions are marked stale
```

### 13.2 Rollback

```text
User chooses rollback target
  |
Create backup
  |
Append DraftRolledBack event
  |
Deactivate active selections after target
  |
Rebuild affected projections
  |
Store redo candidate sequence
  |
Commit
```

Redo is valid only until a new event conflicts with the undone sequence.

### 13.3 Correction

```text
User selects incorrect pick
  |
System rolls back to the correction point
  |
Replacement selection is committed through normal DraftPlayer flow
  |
Original and replacement values are linked by PickCorrected event metadata
  |
Later picks are reviewed or re-entered as needed
```

Yahoo reconciliation uses this same correction path.

### 13.4 Branch Creation

```text
User selects branch point
  |
Create DraftBranchCreated event
  |
Create new branch with parent branch and branch point
  |
Inherit selections before branch point
  |
Switch active branch if requested
  |
Rebuild projections for new branch
```

### 13.5 Yahoo Reconciliation

```text
Yahoo history received
  |
Compare Yahoo picks with local active timeline
  |
Matching picks are marked confirmed
  |
First conflict sets mode to Reconciliation Required
  |
User chooses Use Yahoo or Keep Local
  |
Use Yahoo: run rollback/correction path
  |
Keep Local: record discrepancy, leave active timeline unchanged
```

No separate mutation path exists for reconciliation.

### 13.6 Crash Recovery

```text
Application starts
  |
Open SQLite database
  |
Find unfinished draft
  |
Load active branch
  |
Rebuild or validate projections from events
  |
Run integrity checks
  |
Restore draft room state
```

If validation fails, the UI reports concrete inconsistencies and does not invent missing state.

---

## 14. Draft Source Mode

The draft room displays an operational mode:

- `YahooSynchronized`
- `Manual`
- `YahooReconnecting`
- `ReconciliationRequired`

This is separate from event source. A single manual pick in a Yahoo-hosted draft does not automatically change the overall mode.

Each pick records its own source:

- `Manual`
- `Yahoo`
- `Keeper`
- `Simulation`

---

## 15. Analytics Design

Analytics are deterministic and local.

Initial analytics:

- Current pick and next user pick.
- Picks until user's next pick.
- Available players by position.
- Drafted players by position.
- Recent picks by position.
- Roster requirements filled and remaining.
- ADP value at current pick.
- Ranking value at current pick.
- League-scored projected fantasy points.
- Remaining tier counts.
- Basic alerts.

Superflex and multi-QB analysis should derive QB demand from roster slots:

```text
QB demand = required QB slots + flex slots where QB is eligible
```

Later analytics can add replacement-level value, return-to-next-pick probabilities, and scenario comparison.

---

## 16. UI Design

### 16.1 Main Navigation

Initial screens:

- League Selection
- League Setup
- Yahoo Import Review
- Draft Order Editor
- Keeper Editor
- Player Data Sources and Cache Status
- AI Provider Configuration
- Draft Readiness
- Draft Room
- Draft History
- Scenario/Branch Selection
- Post-Draft Grade and Recap

### 16.2 Draft Room Layout

The Draft Room is the primary product surface.

Recommended layout:

```text
Top status bar:
League, round, pick, clock, selecting team, user's next pick,
draft-source mode, sync health, data freshness.

Left pane:
Draft board and recent picks.

Center pane:
Available players with search, filters, status indicators,
ADP, rankings, tiers, projections, and draft action.

Right pane:
AI analysts with provider/model/status/version/latency.

Bottom band:
My Queue, significant alerts, user roster summary.
```

Critical draft-entry controls must remain visible even when AI panels are loading or unavailable.

### 16.3 Keyboard Operations

Initial shortcuts:

- `/` focuses player search.
- `Enter` drafts selected player after validation.
- `Esc` clears search.
- `Ctrl+D` drafts the top queued player through normal confirmation/commit flow.
- `Ctrl+Z` starts rollback workflow.
- `Ctrl+Shift+Z` and `Ctrl+Y` redo when valid.

---

## 17. Readiness Check Design

The readiness screen produces statuses:

- `Ready`
- `Attention`
- `Failed`
- `Optional`
- `Disabled`

Checks include:

- League configuration.
- Draft order.
- Keepers.
- Local player database.
- Rankings.
- ADP.
- Projections.
- Player status timestamp.
- Yahoo authentication and access.
- AI provider connection tests.
- Active draft database health.
- Latest backup.
- Manual fallback availability.

Readiness should distinguish critical failures from reduced-quality conditions. Missing Yahoo sync should not block a manual draft if local data and draft order are usable.

---

## 18. Credential Storage

Credentials are stored through an abstraction:

```csharp
public interface ICredentialStore
{
    Task SaveSecretAsync(string scope, string key, string secret);
    Task<string?> GetSecretAsync(string scope, string key);
    Task DeleteSecretAsync(string scope, string key);
}
```

Implementations:

- Linux Secret Service-compatible store.
- Windows Credential Manager.
- Explicit warning/fallback implementation only if secure storage is unavailable.

SQLite stores provider configuration and secret references, not raw API keys or OAuth refresh tokens.

---

## 19. Logging and Diagnostics

Logs should include:

- Application startup and shutdown.
- Database migration status.
- Draft operation success/failure.
- Provider connectivity state.
- Yahoo sync status.
- Data refresh status.
- AI request metadata and latency.
- Integrity validation failures.

Logs must not include:

- API keys.
- OAuth tokens.
- Full provider authorization headers.
- Sensitive credential-store payloads.

---

## 20. Error Handling

Critical draft errors:

- Prevent UI advancement.
- Show a clear user-visible failure.
- Leave the previous committed draft state intact.

Provider errors:

- Mark provider status as failed or stale.
- Preserve cached data.
- Do not block draft recording.

AI errors:

- Mark provider panel failed.
- Preserve draft state.
- Continue with deterministic analytics and any remaining AI providers.

---

## 21. Initial Milestone Design

The first useful milestone should avoid Yahoo live-draft dependency.

Included:

- Avalonia application shell.
- SQLite schema and migrations.
- Manual league creation.
- Roster and scoring configuration.
- Draft order editor.
- Keeper editor with one-keeper-per-team validation.
- Local player cache with seed/simple data provider.
- ADP round.pick conversion.
- Manual draft engine.
- Draft board.
- Available-player list.
- Draft queue.
- Team roster projection.
- Continuous persistence.
- Crash recovery.
- Rollback and redo.
- Basic branch creation and switching.
- Deterministic analytics.
- One AI provider.
- Decision-context generation.
- Data freshness display.

Deferred:

- Yahoo OAuth.
- Yahoo league import.
- Yahoo live draft monitoring.
- Yahoo reconciliation.
- Multiple AI providers.
- Optional MCP server.
- Advanced return-to-next-pick probability.
- Full scenario comparison.

---

## 22. Testing Strategy

### 22.1 Core Unit Tests

Prioritize tests for:

- Draft slot generation.
- Snake, linear, and custom order behavior.
- Superflex roster interpretation.
- One-keeper-per-team validation.
- Player availability.
- Draft pick validation.
- Rollback to any previous pick.
- Redo invalidation after new selection.
- Branch independence.
- ADP round.pick conversion.
- Scoring-rule projection calculation.

### 22.2 Persistence Tests

Prioritize tests for:

- Transaction rollback on failed draft command.
- Event persistence.
- Projection rebuild from event history.
- Crash recovery reconstruction.
- Duplicate-player prevention.
- Backup creation around rollback/branch operations.

### 22.3 Integration Tests

Prioritize tests for:

- Manual draft workflow end to end.
- Seed fantasy-data refresh into cache.
- AI decision-context generation from known draft state.
- Provider failure isolation.

### 22.4 UI Tests

Prioritize smoke tests for:

- League setup.
- Draft room loading.
- Manual pick entry.
- Queue add/remove/reorder.
- Rollback workflow.
- Readiness screen status display.

---

## 23. Key Integrity Invariants

The application must continuously enforce:

- A player cannot be active on two teams in the same branch.
- A player cannot be drafted twice in the same active branch.
- A draft slot cannot have more than one active selection.
- A pick cannot be acknowledged until committed.
- The current pick must follow the active timeline.
- Keeper players are unavailable for normal draft selection.
- A team cannot have more than one keeper.
- Rollback restores player availability.
- Redo cannot replay conflicting selections.
- Branches remain independent after their branch point.
- Yahoo reconciliation uses rollback/correction primitives.
- AI cannot mutate critical draft state.

---

## 24. Open Design Decisions

The following decisions should be resolved during implementation or prototyping:

- SQLite migration tool selection.
- Exact event/projection schema shape.
- Avalonia navigation framework and layout composition.
- Preferred secure credential library for Linux and Windows.
- Initial seed/simple fantasy-data source.
- First AI provider and model configuration UI.
- Whether projection tables are updated incrementally, rebuilt on demand, or both.
- How much draft board history remains visible in the first Draft Room prototype.
- Backup retention count and storage location.
- Exact readiness thresholds for stale rankings, ADP, projections, and status.

---

## 25. Implementation Order

Recommended build sequence:

1. Create solution and project structure.
2. Define core domain IDs, entities, value objects, and enums.
3. Implement draft slot generation.
4. Implement SQLite schema and migration path.
5. Implement event append and projection rebuild.
6. Implement manual draft command flow.
7. Build minimal draft room around manual picks.
8. Add queue and roster projections.
9. Add rollback, redo, and correction.
10. Add branch creation and branch switching.
11. Add local player/fantasy-data cache and seed provider.
12. Add deterministic analytics and alerts.
13. Add readiness screen.
14. Add one AI adapter and decision-context generation.
15. Add post-draft deterministic recap.
16. Prototype Yahoo import and live sync as a later milestone.

---

## 26. Risks

| Risk | Mitigation |
| --- | --- |
| Draft event model becomes too complex | Keep first milestone focused on manual draft, rollback, redo, and basic branches before Yahoo sync |
| UI becomes cluttered | Prototype Draft Room early and test under timed-pick workflows |
| Yahoo API behavior is incomplete or slow | Treat Yahoo as non-authoritative input and keep manual entry always available |
| Provider data identifiers do not match cleanly | Use internal player IDs plus provider mapping review tools |
| AI latency is too high | Use precomputed decision context, streaming, fast mode, and parallel providers |
| AI cost surprises user | Track usage and enforce configurable spending limits |
| Crash recovery misses derived state | Treat projections as rebuildable from events and validate on startup |

---

## 27. Design Summary

The core of Fantasy Draft Assistant is a local, durable draft engine backed by SQLite. Draft changes are committed as events inside transactions, then projected into fast read models for the UI, analytics, and AI query services.

External services improve the experience but do not own draft state. Yahoo supplies import and live pick observations. Fantasy-data providers refresh the local cache. AI providers analyze structured read-only draft context. Manual entry remains available at all times.

This design supports the first useful milestone without Yahoo live-draft integration while preserving the architecture required for Yahoo synchronization, reconciliation, multiple AI providers, MCP, and advanced analytics later.
