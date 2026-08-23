# Fantasy Draft Assistant

Desktop draft assistant for Yahoo-hosted and in-person fantasy football drafts. Local SQLite is the source of truth. Yahoo, fantasy-data providers, and AI are replaceable adapters.

First milestone: manual Superflex drafts, durable event history, rollback/redo/branches, seed player cache, deterministic analytics, and one xAI advisor.

## Requirements

- .NET 10 SDK
- Linux (Omarchy / Hyprland is the primary target) or Windows 11

## Run

```bash
dotnet test
dotnet run --project src/FantasyDraftAssistant.App
```

On first launch, use **Start 12-team Superflex mock draft** to refresh live Sleeper player data (falls back to the offline seed if the network is down) and open the Draft Room. Or open **Player Data** and refresh Sleeper yourself.

FantasyPros is optional. On **Player Data**, paste a public API key and refresh. The app stays under the premium limits (1 request/second, 500/day). FantasyPros ranks and tiers win over Sleeper when both are cached.

Yahoo league import is on the **Yahoo** page. You need a Yahoo Fantasy API app (apply at [sports.yahoo.com/developer/access/](https://sports.yahoo.com/developer/access/)), then save the Client ID/secret and sign in. Import pulls teams, roster slots, and scoring. Review draft order and keepers locally. Live Yahoo pick sync is not in this slice.

## Layout

```text
src/FantasyDraftAssistant.App            Avalonia UI
src/FantasyDraftAssistant.Core          Draft engine, analytics, contracts
src/FantasyDraftAssistant.Data          SQLite, migrations, application services
src/FantasyDraftAssistant.Providers.FantasyData
src/FantasyDraftAssistant.Providers.Yahoo
src/FantasyDraftAssistant.Providers.AI  xAI adapter (https://api.x.ai/v1)
```

Local data lives in `~/.local/share/fantasy-draft-assistant/` unless you point it elsewhere. To share leagues across computers (one writer at a time), put the folder on Synology Drive and tell the app where it is:

1. Close the app.
2. Copy `draft.db` and `team-portraits/` into a Drive folder that is **not** the code backup. A sibling such as `~/SynologyDrive/fantasy-draft-assistant/` is enough.
3. Write that path as the only non-comment line in `~/.config/fantasy-draft-assistant/data-root`. `~` is expanded. `#` lines are ignored.
4. On each other computer, wait until Drive is idle, then either `cat HOW-TO` in that folder or run `./setup-this-computer.sh` (writes `data-root` for this machine's Drive path).
5. Re-enter API keys once per computer. Keys are machine-local (OS secret store, or `credentials.dat` encrypted with this machine's name) and will not unlock elsewhere. The setup script reminds you of that.

`FANTASY_DRAFT_ASSISTANT_DATA` overrides the config file for one process. The **Readiness** page shows the resolved directory.

Close the app and wait for Drive to go idle before opening it on another computer. On quit the app checkpoints SQLite WAL so Drive copies one complete `draft.db` instead of a live `-wal` sidecar.

xAI keys are stored through Linux Secret Service when `secret-tool` is available, otherwise a file-based fallback with a readiness warning. Set the key in **AI Providers**. Default model: `grok-4.6`.

The public draft-board URLs (`/draft/<league>/` on kellynorton.com) are nginx config in `deploy/nginx/`. The app PUTs `board.json` and `index.html` there when a league has publishing enabled (League Setup) and a bearer token is saved (Player Data).

## Keyboard (Draft Room)

| Shortcut | Action |
| --- | --- |
| `/` | Focus player search |
| Enter | Draft selected player |
| Esc | Clear search |
| Ctrl+D | Draft top queued player |
| Ctrl+Z | Roll back last pick |
| Ctrl+Y / Ctrl+Shift+Z | Redo |

## Documents

- `Fantasy Draft Assistant — Software Requirements Specification v0.4.md`
- `Fantasy Draft Assistant - Software Design Document v0.1.md`
