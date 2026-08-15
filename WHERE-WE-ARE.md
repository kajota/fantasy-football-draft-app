# Where we are — 2026-08-15

Uncommitted work on `master` (ahead of `63acb60`). Spec is still SRS v0.4 / SDD v0.1. Requirements were drafted in Claude/ChatGPT; implementation has been in Grok.

Run:

```bash
dotnet test
dotnet run --project src/FantasyDraftAssistant.App
```

Local data: `~/.local/share/fantasy-draft-assistant/` (`draft.db`). FantasyPros key is in the credential store, not git. Do not paste that key into chat.

---

## First milestone (manual draft)

Done and dogfooded:

- League create / archive / permanent delete (type-the-name confirm)
- League Setup (teams, roster presets, Superflex)
- Draft Order editor
- Keepers (one per team, crash + invalid-round fixes)
- Draft Room: search, sort, queue add/remove/reorder, Draft first (Ctrl+D), undo/redo
- Branches (create + switch)
- Crash after undo-then-redraft: pick saved, then UI reload cleared selection — name captured first; reload muted during Draft / Draft first / Undo / Redo
- History: names/teams/source, not player GUIDs
- Recap: named rosters, ADP vs pick, last pick already marks draft complete
- Readiness, AI provider settings (xAI default `grok-4.6`), seed player cache

---

Player Data cards show last sync in **local time**, with the timezone named on Cache contents (those stamps were UTC and unlabeled before).

---

## Player data

| Source | Role |
| --- | --- |
| **Sleeper** | Free live players, 2QB/PPR ADP, projections. Mock draft uses this. |
| **FantasyPros** | Premium key. Expert ranks + ADP for the **open league’s closest sheet**. |
| **Seed** | Offline fallback |

FantasyPros:

- Key saved in the app (Player Data). Limits: 1 req/s, 500/day. A refresh is ~4 calls.
- **Does not** import Yahoo leagues. Public API is ranks/projections/players only.
- Ranks/ADP use Standard / Half PPR / PPR and 1-QB vs Superflex (`OP`). That is chosen from **this league’s scoring + roster**, not hardcoded Superflex.
- Projections are re-scored with the league’s exact point values.
- Draft Room combo: FantasyPros (format label) vs Sleeper. Cache only; no network mid-pick.
- Superflex ADP exists on FP (`type=ADP`, `position=OP`, Allen ~3) but we fetch the sheet that matches the league.

After changing scoring or Superflex: **Save** on League Setup, then **Player Data → FantasyPros → Refresh**.

---

## Scoring

**League Setup → Scoring** has Standard / Half PPR / PPR presets plus editable passing, rushing, receiving, turnovers, kicking, defense.

Kicking is Yahoo-style FG bands: 0–19 / 20–29 / 30–39 = 3, 40–49 = 4, 50+ = 5, PAT = 1, extra point returned = 2.

Scoring writes as soon as a value or preset changes. Save still stores name, roster, teams, and scoring, and shows a green “Saved.” next to the button.

Every scoring row now says what the number means (per yard, per catch, each FG, DST bonus). Reception is PPR. Yahoo “10 yards per point” is Receiving yards = 0.10, not Reception.

Save writes `SaveScoringAsync`. Invalid numbers block save. Old leagues that only had a flat Field Goal fill the short bands from that value and keep 4 / 5 for the long ones.

Yahoo import maps stat IDs 19–23 to those FG bands (19 is FG 0–19, not a 2-pt conversion). DST sack/INT/FR/TD/safety IDs were off by one and are corrected.

FantasyPros still has no custom-scoring sheet (e.g. 0.3 PPR). Closest official sheet + local projection scoring. Kicker projections are still not FG-by-distance (providers give totals).

---

Draft Room now has Overview plus tabs for Board / Available / Queue / Roster / AI. Conversation is saved per draft branch. Rank source combo honors the user's pick (it used to snap back to the league FantasyPros sheet). `/` and `?` no longer steal focus from the AI box.

---

## Draft Room AI (just added)

Ask already existed. It was using the generic last-refresh FantasyPros cache (`fantasypros`), so Superflex ranks could leak into a 1-QB league.

Now:

- Decision context picks the **same sheet as the board** (`fantasypros-half`, `fantasypros-ppr-sf`, …). It will not fall back to a different FP format sheet.
- Context includes `scoringProfile`, `scoringLines`, `rankingsSource`, `myRemainingNeeds`, `recentPositions`.
- `ProjectedPoints` are league-scored. Projections stored only under generic `fantasypros` still load via fallback.
- Fast-mode prompt asks for a 3-player shortlist, one pick, and whether they last until your next pick.
- Draft Room AI card names the sheet it is using.

Alerts, Recap ADP vs pick, and the available-player list use that same league sheet.

---

## Yahoo

- Adapter exists: OAuth 2.0, league list, import teams/roster/scoring, no live pick sync.
- Auction leagues rejected. Keepers/draft order not overwritten on refresh unless asked.
- **Blocked:** Yahoo access review, 1–2 weeks. Test import when the email arrives.
- Redirect to register: `http://127.0.0.1:8765/yahoo/callback`
- Apply: https://sports.yahoo.com/developer/access/
- Yahoo player ids (~869) already stored from FantasyPros for later mapping.

---

## When you sit down again

1. Restart the app. Open a **1-QB** league if you want 1-QB ranks (new league default is 1-QB Half PPR; the mock is Superflex).
2. League Setup → confirm **Scoring**. Reception is PPR (0 / 0.5 / 1). Receiving yards should be 0.10 if Yahoo says “10 yards per point.” FG bands should match Yahoo 3/3/3/4/5. Save.
3. Player Data → FantasyPros → Refresh. Status should name the sheet (e.g. Half PPR 1-QB).
4. Draft Room: Gibbs/Bijan/Chase at the top of Rk for 1-QB; flip Sleeper to compare.
5. Enable Grok under AI Providers if it is not already. Ask “Who should I take here?” The card should say **FantasyPros Half PPR 1-QB**. Advice should not treat it as Superflex.

---

## Still open (not blocking)

- Live Yahoo draft poll / reconcile / pick clock
- FantasyPros Superflex ADP as a separate optional sheet
- History event log (started/rollback), not only active picks
- Recap AI narrative
- Visual polish
- Auto-ask when you go on the clock / stale-response refresh

Next product work: wait on Yahoo, Recap AI narrative, or dogfood the Draft Room ask against a real 1-QB league.
