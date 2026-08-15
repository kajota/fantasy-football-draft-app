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

Local data lives in `~/.local/share/fantasy-draft-assistant/`.

xAI keys are stored through Linux Secret Service when `secret-tool` is available, otherwise a file-based fallback with a readiness warning. Set the key in **AI Providers**. Default model: `grok-4.6`.

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
