# Where we are — 2026-08-16

On `master` at `b98abce` (pushed). Spec is still SRS v0.4 / SDD v0.1. Requirements were drafted in Claude/ChatGPT; implementation has been in Grok.

The app is dogfoodable for manual and practice drafts. Yahoo live sync is the main missing spec item, and it is blocked on Yahoo access review.

Run:

```bash
dotnet test
dotnet run --project src/FantasyDraftAssistant.App
```

Local data: `~/.local/share/fantasy-draft-assistant/` (`draft.db`). FantasyPros key is in the credential store, not git. Do not paste that key into chat.

---

## What is in

Leagues and setup:

- Create league with **8 / 10 / 12 / 14 / 16** teams (default 12). Mock button is still 12-team Superflex.
- League cards show `N teams · Season · draft status`.
- League Setup: name, season, rounds, AI draft guidelines, roster presets, scoring (Yahoo FG bands), team names/owners/portraits.
- Per-team **About** box (`PortraitNotes`, migration `009`) feeds image generation. Empty box uses the original default look (including the old name-based woman check).
- First-round seats: `#` box, ↑/↓, and drag the `≡` handle. **Save** rewrites live draft slots when seats are still editable.
- Seats lock only when the **live** board has regular (non-keeper) picks. Returning to an empty live board unlocks them again. Practice picks do not lock live order.
- Page Up / Page Down scroll League Setup even when a text box is focused.

Draft Room:

- Opening a league attaches **that** league’s draft (`SessionDraft`). Opening Draft Room creates and starts a draft if the league has none. A leftover branch id from another league is ignored.
- Search, sort, queue, board, undo/redo, branches, practice CPU seats.
- **Auto-ask is off** until you check it. Opening the room does not spend AI money.
- Same-position **shared bye** badge (`Bye 8 · Gibbs`) on available players and the queue.
- Empty IR / bench rows say **Open**, not Need. Need is starters only.
- Practice: after your last pick, CPU keeps going (K/DEF in the last rounds). Button becomes **Finish remaining picks**.

Recap:

- Letter grades are **relative to this league** (size, Superflex vs 1-QB, this scoring). A typical filled roster lands around **B**.
- 12-team ADP is scaled to this team count (`24` → expected pick `20` in a 10-team).
- Superflex without a FantasyPros Superflex cache uses **Sleeper 2QB ADP**, not 1-QB ranks.

---

## Player data

| Source | Role |
| --- | --- |
| **Sleeper** | Free live players, 2QB/PPR ADP, projections. Fallback for Superflex ADP if the FP Superflex sheet is missing. |
| **FantasyPros** | Premium key. Expert ranks + ADP for the **open league’s closest sheet**. |
| **Seed** | Offline fallback |

After changing scoring or Superflex: **Save** on League Setup, then **Player Data → FantasyPros → Refresh**. Status should name the sheet (e.g. Half PPR Superflex).

FantasyPros still has no custom-scoring sheet (e.g. 0.3 PPR). Closest official sheet + local projection scoring.

---

## Scoring (quick check)

Reception is PPR (0 / 0.5 / 1). Yahoo “10 yards per point” is Receiving yards = 0.10. FG bands should be 3/3/3/4/5.

Scoring saves as you edit. League Save stores name, guidelines, roster, teams/seats, and scoring.

---

## Yahoo

- Adapter exists: OAuth 2.0, league list, import teams/roster/scoring. No live pick sync.
- Auction leagues rejected. Keepers/draft order not overwritten on refresh unless asked.
- **Blocked:** Yahoo access review. Test import when the email arrives.
- Redirect: `http://127.0.0.1:8765/yahoo/callback`
- Apply: https://sports.yahoo.com/developer/access/
- Yahoo player ids (~869) already stored from FantasyPros for later mapping.

---

## When you sit down again

1. `dotnet test` then `dotnet run --project src/FantasyDraftAssistant.App`.
2. Open the league you care about (10-team Superflex or 1-QB). Confirm League Setup scoring + roster, Save.
3. Player Data → FantasyPros → Refresh so the sheet matches that league.
4. Draft Room: Auto-ask stays off unless you check it. Practice from here if you want a CPU run; Live draft returns to the real board.
5. After a complete draft, Recap letters should spread around B, not a row of C/F.

---

## Still open (not blocking)

- Live Yahoo draft poll / reconcile / pick clock (blocked on API approval)
- Recap AI narrative (spec §66.2 — deterministic grades exist, no provider write-up)
- History event log (started/rollback), not only the active timeline
- FantasyPros Superflex ADP as its own optional extra sheet (league sheet is already fetched; Sleeper 2QB is the fallback)
- Visual polish
- Stale-AI-response refresh while auto-ask is on

Likely next product work if anything: wait on Yahoo, Recap AI narrative, or one more real-league dogfood pass.
