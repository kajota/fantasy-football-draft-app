# Fantasy Draft Assistant

Desktop app for running or shadowing a fantasy football snake draft. You sit next
to the real draft (Yahoo, in-person, or a practice run) and record every pick
here. Local SQLite holds leagues, picks, and the player cache. Yahoo, Sleeper,
FantasyPros, and the AI providers are optional adapters.

It is an Avalonia desktop app on .NET 10. There is no installer and no web UI
for the assistant itself — you clone this repo and run it. Version in the
sidebar comes from git tags (`v1.0.5` at the time of writing).

## What it does

- **Leagues.** Create an 8 / 10 / 12 / 14 / 16 team snake league, 1-QB or
  Superflex. Archive or permanently delete later. Season defaults to 2026.
- **Yahoo import.** Paste the league Settings and Managers pages from your
  browser (no API key). Optional Yahoo Fantasy API import if you have an
  approved developer app. Auction leagues are rejected. Live Yahoo pick sync
  is not implemented — you still enter picks by hand.
- **League Setup.** Name, season, rounds, roster slots, scoring (Standard /
  Half PPR / PPR presets plus Yahoo FG bands), first-round seats, which team
  is yours, per-team CPU style for practice, and team portraits.
- **Keepers.** One keeper per team, consuming that team's pick in a chosen
  round.
- **Draft Room.** Search, sort, queue, draft, undo/redo, reset the board,
  ADP-colored board, roster needs, handcuffs, shared-bye warnings, injury
  status from Sleeper.
- **Practice drafts.** Fork the live board without changing it. CPU seats pick
  from rankings using a personality (Best available, Zero RB, Hero RB, …).
  Pin **AI drafter** on a seat to have a model draft that team.
- **AI advisors.** Independent ChatGPT, Claude, and Grok panels. Fast / Deep
  ask, optional auto-ask when you are on the clock, a Draft Watcher that
  speaks on board events. Auto-ask is **off** until you check it.
- **Recap.** Letter grades relative to this league (team count, Superflex vs
  1-QB, this scoring). No AI required. A typical filled roster lands around
  **B**. Kicker and defense do not affect the grade.
- **Web board (optional).** After each pick the app can PUT `board.json` to a
  public URL for a TV / phone view.

## What it does not do

- Live Yahoo draft polling or pick clocks
- Auction drafts
- A packaged Windows/Linux installer or app store build
- Recap write-ups from an AI (grades are deterministic)

## Requirements

- **.NET 10 SDK** ([download](https://dotnet.microsoft.com/download/dotnet/10.0))
- A desktop: **Linux** (Omarchy / Hyprland is the development machine) or
  **Windows 10 / 11**
- Network for Sleeper / FantasyPros / AI / Yahoo. Offline, the built-in seed
  pack still lets you click around.

Optional:

- A [FantasyPros](https://api.fantasypros.com/) public API key (premium
  personal keys: 1 request/second, 500/day)
- Developer API keys for OpenAI, Anthropic, and/or xAI (subscription plans
  such as ChatGPT Plus, Claude Pro, and SuperGrok do **not** include these)
- On Linux, `secret-tool` (from `libsecret`) so API keys go in the OS
  keyring instead of a machine-bound file
- A Yahoo Fantasy API app, only if you want the API import tab instead of
  paste ([apply](https://sports.yahoo.com/developer/access/))

## Install

There is no binary release. Install the SDK, clone the repo, run from source.

### Linux

1. Install the .NET 10 SDK.

   On Arch / Omarchy:

   ```bash
   sudo pacman -S dotnet-sdk
   ```

   Confirm you actually have 10.x (`dotnet --list-sdks`). Distro packages
   sometimes lag; if you only have 8 or 9, use Microsoft's
   [install script](https://learn.microsoft.com/dotnet/core/install/linux)
   or their package repo.

2. Optional keyring (recommended):

   ```bash
   sudo pacman -S libsecret
   ```

   Debian / Ubuntu: `sudo apt install dotnet-sdk-10.0 libsecret-tools`

3. Clone and build:

   ```bash
   git clone <this-repo-url> ff-draft-app-grok
   cd ff-draft-app-grok
   dotnet test
   ```

A desktop launcher lives at `deploy/linux/fantasy-draft-assistant.desktop`.
Copy it into `~/.local/share/applications/`, then edit `Exec=` to point at
`scripts/launch-fantasy-draft-assistant.sh` in **this** clone (the checked-in
file still has another machine's path). That script sets
`AVALONIA_GLOBAL_SCALE_FACTOR=1.25` for HiDPI and runs the app.

### Windows

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
   (x64). A reboot is sometimes needed before `dotnet` is on PATH.
2. Clone with Git for Windows or GitHub Desktop.
3. In PowerShell or Command Prompt:

   ```powershell
   cd path\to\ff-draft-app-grok
   dotnet test
   ```

Windows has no Secret Service integration. Keys are stored in
`%USERPROFILE%\.local\share\fantasy-draft-assistant\credentials.dat`,
encrypted with a key derived from this machine's user and computer name.
The **Readiness** page will say the file-based fallback is in use. That is
expected.

## Run

From the repo root, same command on both OS:

```bash
dotnet run --project src/FantasyDraftAssistant.App
```

Linux shortcut (HiDPI scale 1.25):

```bash
./scripts/launch-fantasy-draft-assistant.sh
```

First launch can take a bit while NuGet packages restore. Later launches
are faster. The sidebar footer shows the version.

To run tests without starting the UI:

```bash
dotnet test
```

## Data on disk

The app always uses these path shapes, **including on Windows** (it does not
use `%AppData%`).

| What | Default location |
| --- | --- |
| Database, portraits, backups, lock | `~/.local/share/fantasy-draft-assistant/` (`%USERPROFILE%\.local\share\fantasy-draft-assistant` on Windows) |
| Pointer to a different data folder | `~/.config/fantasy-draft-assistant/data-root` |
| API keys (file fallback) | same as the data folder unless you override credentials |

Inside the data folder:

- `draft.db` — leagues, drafts, player cache
- `team-portraits/` — generated or imported team images
- `backups/` — rolling SQLite copies (kept to 20). Permanent delete of a
  league writes one first.
- `draft.lock` — advisory lock while the app is open

Resolution order for the data folder: `FANTASY_DRAFT_ASSISTANT_DATA` (one
process) → the `data-root` config file → the default above. `~` is expanded.
`#` lines in the config file are ignored. Credentials can be pointed
elsewhere with `FANTASY_DRAFT_ASSISTANT_CREDENTIALS`. **Readiness** shows
the resolved directory.

### Sharing one database across computers

League data can live on Synology Drive (or similar) so two machines see the
same leagues. **One writer at a time.** API keys stay on each machine and
will not unlock on the other.

1. Close the app.
2. Copy `draft.db` and `team-portraits/` into a Drive folder that is **not**
   the code backup. A sibling such as `~/SynologyDrive/fantasy-draft-assistant/`
   is enough.
3. Put that path as the only non-comment line in
   `~/.config/fantasy-draft-assistant/data-root`.
4. On the other computer, wait until Drive is idle, then write the matching
   path for *that* machine into the same config file.
5. Re-enter API keys once per computer.

Close the app and wait for Drive to go idle before opening it elsewhere. On
quit the app checkpoints SQLite WAL so Drive copies one complete `draft.db`
instead of a live `-wal` sidecar.

While open, the app writes `draft.lock` next to `draft.db` and refreshes it
every 30 seconds. A second computer that sees a fresh lock gets **Quit** or
**Open anyway**. A lock with no heartbeat for 5 minutes is treated as stale.
This is advisory: Drive sync is not instant, so two copies started at the
same moment can still collide. If another machine takes the lock while you
are running, a red banner tells you to stop.

## First session

1. Start the app. **Leagues** is the home page. There is no one-click
   “12-team Superflex mock” anymore — create a league or import one.
2. **Player Data.** Refresh **Sleeper** (free, no key). That fills NFL
   players, 2QB/PPR ADP, projections, and injury status. Optionally paste a
   FantasyPros key and refresh so expert ranks and tiers win over Sleeper.
   If the network is down, refresh **Built-in seed** instead.
3. Create a league (name, team count, Superflex checkbox) **or** import from
   **Yahoo**. Creating a league jumps to League Setup.
4. On **League Setup**, set scoring and roster to match the real league,
   mark **Mine** on your team, set first-round seats, Save. After a scoring
   or Superflex change, go back to Player Data and refresh FantasyPros so
   the cached sheet matches (Standard / Half PPR / PPR × 1-QB or Superflex).
5. Optional: **Keepers**, **Draft Order**.
6. **Draft Room.** Opening it creates and starts a draft if the league has
   none. Auto-ask stays off until you check it.

**Readiness** is the pre-flight list: data folder, lock, player cache age,
keys, backups.

## Pages

| Sidebar | What it is |
| --- | --- |
| **Leagues** | Create, open, archive, permanently delete |
| **League Setup** | Name, season, rounds, AI draft guidelines, roster, scoring, seats, portraits, CPU style, “Mine” |
| **Draft Order** | Snake / Linear, first-round seats, create/start draft |
| **Keepers** | One keeper per team; locked once regular (non-keeper) picks are on the board |
| **Yahoo** | Paste import (default) or API import |
| **Player Data** | Sleeper / FantasyPros / seed refresh, web-board URL and bearer token |
| **AI Providers** | Enable ChatGPT / Claude / Grok, model, role, optional spend cap, test connection |
| **Readiness** | Checklist for the open draft |
| **Draft Room** | The board you live on during a draft |
| **History** | Picks that still count on the active timeline |
| **Branches** | What-if timelines; practice drafts are branches |
| **Post-Draft Recap** | Grades and ADP value; **Mark draft complete** when you are done |

Page Up / Page Down still scroll League Setup when a text box is focused.

## Draft Room

Opening a league attaches **that** league’s draft. A leftover branch id from
another league is ignored. Opening Draft Room creates and starts a draft if
needed.

**Header:** who is on the clock, undo / redo / reset (reset wants a second
click; undo puts the board back). **Practice from here** forks a branch from
the live pick. **Live draft** returns to the real board. On a practice
branch: **Play until my pick** (or **Finish remaining picks** after your
last selection), **Step**, **Pause**.

**Tabs:**

- **Overview** — pick list, available players, and a decision strip: best
  remaining at each position, whether they last until your next pick, tier
  cliffs, and a guess at who picks before you (same policy as practice
  CPUs — useful for position pressure, often wrong on the exact name).
- **Draft List** — every slot in order.
- **Draft Board** — teams across, rounds down. Your column is gold. Optional
  ADP heat (green steal / red reach) or position colors.
- **Available Players** — full list with rank, ADP, projections, tier,
  injury, FantasyPros / Sleeper / Yahoo links. Switch the cache source
  without hitting the network.
- **My Queue** — ordered shortlist; **Draft first** or `Ctrl+D`.
- **Roster** — starting slots, flex, bench, IR. Empty starters are needs;
  empty bench/IR are **Open**. Import/export that team’s portrait here too.
- **AI Analysts** — Fast / Deep / Ask. Empty prompt means “who should the
  person on the clock pick?”, not “who should I take?”. Click a highlighted
  player name in a reply to find them on Available Players.
- **AI History** — saved prompts and replies for this branch; export
  Markdown.

Shared-bye badges (`Bye 8 · Gibbs`) and handcuff highlights show on
available players and the queue. Injury **St** comes from the last Sleeper
refresh (blank means Active). FantasyPros/seed refreshes will not wipe a
Sleeper injury back to Active.

### Practice CPU styles

Set per team on League Setup. Live drafts ignore them.

| Style | Behavior |
| --- | --- |
| Random | Deal from the bag each practice (never deals AI drafter) |
| Best available / RB first / Hero RB / Zero RB / WR heavy / QB early / Late QB / Rookie hunter / Chases ADP | Deterministic ranking policy |
| AI drafter | A model drafts the seat to a hidden strategy (revealed on Recap). Needs an enabled provider with a key. Roughly a couple of cents for a 15-round run on a cheap model. |

### Keyboard

| Shortcut | Action |
| --- | --- |
| `/` | Focus player search |
| Enter | Draft the selected player |
| Esc | Clear search |
| Ctrl+D | Draft the top queued player |
| Ctrl+Z | Roll back the last pick (keepers stay) |
| Ctrl+Y / Ctrl+Shift+Z | Redo |

## Player data

The Draft Room reads the **local cache**, not the network.

| Source | Role |
| --- | --- |
| **Sleeper** | Free. Live players, 2QB/PPR ADP, projections, injury/status. Fallback Superflex ADP if the FantasyPros Superflex sheet is missing. |
| **FantasyPros** | Premium key. Expert ranks, tiers, ADP for the open league’s closest sheet (Standard / Half PPR / PPR × 1-QB or Superflex). Projections are re-scored with **this** league’s exact rules. |
| **Built-in seed** | Offline sample so the UI is usable with no network. |

FantasyPros has no custom-scoring sheet (for example 0.3 PPR). Closest
official sheet plus local projection scoring is the intended setup.

After changing scoring or Superflex: **Save** on League Setup, then
**Player Data → FantasyPros → Refresh**. Status should name the sheet
(e.g. Half PPR Superflex).

Scoring note: Reception is PPR (0 / 0.5 / 1). Yahoo “10 yards per point”
is Receiving yards = `0.10`, not Reception. FG bands on Yahoo default
should be 3/3/3/4/5. Scoring saves as you edit; **Save** on League Setup
also stores name, guidelines, roster, teams/seats.

## AI

On **AI Providers**, enable a provider, paste a **developer** API key, pick
a model, pick a role, Save, then Test connection.

Recommended value models in the dropdown: **gpt-5.6-luna**,
**claude-sonnet-5**, **grok-4.3**. Flagship models are overkill — the app
already ranks the board; the model is reading that context.

| Role | When it speaks |
| --- | --- |
| Fast Advisor | Follows the Fast/Deep buttons in Draft Room |
| Deep Advisor | Always Deep |
| Draft Watcher | Does not answer Ask. Fires when the board changes (runs, value, your pick approaching) |
| Secondary Opinion | Listed, not wired yet |

Optional per-draft USD cap: hitting it never blocks recording a pick.
Failed or disabled providers never block pick entry.

League Setup **Draft guidelines** are plain-language notes the analysts
should follow for *your* picks (House rules, Zero RB, Superflex QB, …).
They do not change the board or the CPU mock.

**Team portraits:** League Setup → **New image** (art style, Normal vs
roast, editable prompt) or **Generate team images**. Grok Imagine or
ChatGPT Images; Claude cannot generate pictures. You can also import a
JPEG / PNG / WebP. Your team is always the “hero” look.

## Yahoo

**Paste from Yahoo** is the path that works without Yahoo approving an
app:

1. Open the league Settings page, Ctrl+A / Ctrl+C, paste.
   `https://football.fantasysports.yahoo.com/f1/<id>/settings`
2. Same on League → Managers (Standings will do if Managers does not list
   owners).
3. Optional league URL/ID so a later API import updates the same league.
4. **Parse and preview** (or **Let the AI read it** if a provider is set
   up), then import.

Review seats and mark **Mine** on League Setup — paste cannot detect which
team is yours and guesses the first one.

**Connect with API:** Client ID/secret from an approved Yahoo app,
redirect URI exactly `http://127.0.0.1:8765/yahoo/callback`, then Sign in.
Imports teams, roster slots, and scoring. Draft order is a guess from
Yahoo team numbers unless you check replace-on-refresh. Keepers are never
overwritten unless you assign them. Auction leagues cannot be imported.

## Web draft board

Optional. For a TV, phone, or Fire Stick view:

1. On **Player Data**, set the base URL (default
   `https://kellynorton.com/draft`) and save the bearer token that matches
   the host’s `draft-token.map`.
2. On **League Setup**, set a slug (`filthymothers`) and check **Publish
   this league’s board to the web**.

The app PUTs `board.json` (and HTML) to `{base}/{slug}/` after picks, with
retries if a publish fails. Server nginx config for kellynorton.com is in
`deploy/nginx/`. `GET` is public; `PUT` needs the token. The host must
already have the slug folder.

## Repo layout

```text
src/FantasyDraftAssistant.App            Avalonia UI (MVVM)
src/FantasyDraftAssistant.Core          Draft engine, analytics, contracts
src/FantasyDraftAssistant.Data          SQLite, migrations, application services
src/FantasyDraftAssistant.Providers.FantasyData   Sleeper, FantasyPros, seed
src/FantasyDraftAssistant.Providers.Yahoo
src/FantasyDraftAssistant.Providers.AI  OpenAI, Anthropic, xAI, portraits
tests/                                  xUnit
deploy/linux/                           .desktop launcher
deploy/nginx/                           public board hosting
scripts/launch-fantasy-draft-assistant.sh
```

Solution file: `FantasyDraftAssistant.slnx`.

## Other documents

- `DRAFT-GRADES.md` — how Recap letters are computed
- `deploy/nginx/README.md` — hosting the public board
- `Fantasy Draft Assistant — Software Requirements Specification v0.4.md`
  and `Fantasy Draft Assistant - Software Design Document v0.1.md` — original
  design; the running app has moved on in places (Yahoo paste, practice AI
  seats, web board, no live Yahoo sync)
- `WHERE-WE-ARE.md` — dated development snapshot, not a user guide
