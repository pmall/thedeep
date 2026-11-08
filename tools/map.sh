#!/usr/bin/env bash
# Prints the map: the built rooms' graph (ways, placements, ability orders) and the whole game's draft
# (the world's network, worlds/prototype/map-draft.md: summary, every way, every achievable order; each room's kinds, from its room table). Fails like the room check does.
# It also writes the network's drawing for the owner to read, lookdev/map-WORLD.svg (MapDrawing; open it in a browser).
#   tools/map.sh                    the prototype, checked from its mechanics lab
#   tools/map.sh WORLD ROOM         another world, checked from one of its rooms without machines (tools/map.sh sequel Strongroom)
set -euo pipefail

cd "$(dirname "$0")/.."
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
GODOT="${GODOT:-godot}"

dotnet build TheDeep.sln -v q -nologo >/dev/null || {
    echo "build failed: run tools/check.sh"
    exit 1
}
from=(--room MechanicsLab)
if [ $# -eq 2 ]; then from=(--world "$1" --room "$2"); fi
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 600 -- "${from[@]}" --validate-rooms --map-report 2>&1 \
    | sed -n '/ROOM CHECK\|ROOMS OK/,$p'
