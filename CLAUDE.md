# Fantasy Draft Assistant

## NEVER RUN DESTRUCTIVE GIT COMMANDS IN THIS REPO

**THIS REPO ALWAYS HAS LARGE AMOUNTS OF UNCOMMITTED WORK IN THE WORKING TREE.
DESTRUCTIVE GIT COMMANDS DESTROY IT PERMANENTLY. IT IS NOT IN THE REFLOG.
IT IS NOT RECOVERABLE FROM GIT.**

**NEVER run any of these without the user explicitly asking for that exact command:**

- `git reset --hard` — **BANNED.** This already destroyed a day of the user's
  uncommitted work on 2026-08-19. Never type `--hard`. Not as a fallback, not
  "just to clean up", not chained after `&&`, not ever.
- `git checkout -- <path>` / `git restore <path>` — discards uncommitted changes.
- `git clean -f` / `-fd` / `-fdx` — deletes untracked files.
- `git stash` (including `stash pop` / `stash drop`) — moves the user's work
  somewhere they did not put it.
- `git rebase`, `git merge`, `git cherry-pick`, force pushes, branch deletion.

See `~/.claude/CLAUDE.md` — this ban is global, not specific to this repo.

**IF A DESTRUCTIVE COMMAND IS GENUINELY THE RIGHT CALL: DO NOT RUN IT.** Say what
should be run, spell out exactly what could be lost (check `git status` first), and
tell the user to run it themselves via `! <command>`. Hand it over; don't do it for them.

**RULES:**

1. To undo a commit you just made, use `git reset --soft HEAD~1`. **SOFT. ONLY SOFT.**
   `--soft` keeps the working tree untouched. `--hard` obliterates it.
2. **NEVER chain a git write command after `&&` or `;`** in a compound Bash line.
   Run it alone so it can be reviewed on its own.
3. Do not create commits (even `--allow-empty`) to test or probe build behavior.
   Find a read-only way, or ask.
4. Before ANY git command that writes, run `git status` and look at what is at risk.
5. When in doubt, ask. The user would rather answer a question than lose work.

The user keeps a manual copy at `~/SynologyDrive/ff-draft-app-grok`. **That is the
user's backup, not a scratch space — never write to it, never run git commands in
it.** Its existence is not a license to be careless here.

## Project

Avalonia desktop app targeting .NET 10, C# with nullable enabled and
`TreatWarningsAsErrors`. Central package management via `Directory.Packages.props`.

- `src/FantasyDraftAssistant.Core` — domain models, draft engine, AI prompt logic
- `src/FantasyDraftAssistant.Data` — SQLite persistence, migrations, query services
- `src/FantasyDraftAssistant.Providers.*` — FantasyData, Yahoo, and AI provider adapters
- `src/FantasyDraftAssistant.App` — Avalonia UI (MVVM via CommunityToolkit.Mvvm)
- `tests/` — xUnit

Build: `dotnet build`  Test: `dotnet test`
