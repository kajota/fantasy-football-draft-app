#!/usr/bin/env bash
set -euo pipefail

project_root="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$project_root"

export AVALONIA_GLOBAL_SCALE_FACTOR=1.25
exec dotnet run --project src/FantasyDraftAssistant.App
