#!/usr/bin/env bash
# Prints the map: the built rooms' graph (ways, placements, ability orders) and the whole game's draft
# (docs/map-draft.md: summary, every way, every achievable order). Fails like the room check does.
#   tools/map.sh
set -euo pipefail

cd "$(dirname "$0")/.."
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
GODOT="${GODOT:-godot}"

dotnet build TheDeep.sln -v q -nologo >/dev/null || { echo "build failed: run tools/check.sh"; exit 1; }
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 600 -- --room MechanicsLab --validate-rooms --map-report 2>&1 \
    | sed -n '/ROOM CHECK\|ROOMS OK/,$p'
