#!/usr/bin/env bash
# Look-dev captures: renders every photo spot and reel of the rooms in ROOMS (every room of the prototype, the machine arena,
# the mechanics lab; with --world, every room of that world) to PNG at 1080p, measures frame times over a scripted run in each, and compares with the previous
# run (before/after sheet). A run's folder holds <Room>-<Spot>.png, the reels' contact sheets (Motion, Combat, Strikes, Grip, Swim,
# Harpoon, Bore, Machines, Moments; the interactables used: Door, Taking, Pickup, Lever .png), perf.json (the first room) and
# perf-<Room>.json (frame, GPU and render CPU times, garbage collections, the slowest frames), run.txt, the Godot logs, and
# compare/ (one image per spot, sheet.png with all).
#
#   tools/capture.sh [label]                 new run in lookdev/<date>_<time>[-label]/, compared with the latest run
#   tools/capture.sh [label] --against DIR   compare with DIR instead
#   tools/capture.sh [label] --biome NAME    render with resources/biomes/NAME.tres instead of each room's own
#   tools/capture.sh [label] --rooms "A B"   render only these rooms, quoted as one argument (quick looks while building a room)
#   tools/capture.sh [label] --world NAME    render worlds/NAME's rooms instead (all of them, or --rooms of them): --world sequel
#   tools/capture.sh [label] --look NAME     render in the drawn look NAME (resources/looks/NAME.tres, as `--look` in the game)
#   tools/capture.sh --compare BEFORE AFTER  only build the sheet for two existing runs
#
# Opens a small window: about 8 minutes for every room, under a minute for one with --rooms (rendering needs a GPU). See instructions/testing.md#lookdev.
set -euo pipefail

cd "$(dirname "$0")/.."

# shellcheck source=tools/env.sh
source tools/env.sh
GODOT="${GODOT:-godot}"
ROOT="lookdev"
SIZE="1920x1080"

fail() {
    printf '\033[31mCAPTURE FAILED: %s\033[0m\n' "$1"
    exit 1
}

compare() {
    local before="$1" after="$2"
    printf '\n\033[1mbefore/after: %s → %s\033[0m\n' "$(basename "$before")" "$(basename "$after")"
    python3 -I tools/lookdev_compare.py "$before" "$after" "$after/compare"
}

if [ "${1:-}" = "--compare" ]; then
    [ $# -eq 3 ] || fail "usage: tools/capture.sh --compare BEFORE AFTER"
    compare "${2%/}" "${3%/}"
    exit 0
fi

USAGE='usage: tools/capture.sh [label] [--against DIR] [--biome NAME] [--rooms "A B"] [--world NAME] [--look NAME] (several rooms in one quoted argument)'
label="" against="" biome_args=() world_args=() look_args=() only=""
while [ $# -gt 0 ]; do
    case "$1" in
        --against | --biome | --rooms | --world | --look)
            [ $# -ge 2 ] && [ -n "$2" ] && [ "${2#-}" = "$2" ] || fail "$1 needs a value; $USAGE"
            ;;&
        --against)
            against="${2%/}"
            shift 2
            ;;
        --biome)
            biome_args=(--biome "$2")
            shift 2
            ;;
        --rooms)
            only="$2"
            shift 2
            ;;
        --world)
            world_args=(--world "$2")
            shift 2
            ;;
        --look)
            look_args=(--look "$2")
            shift 2
            ;;
        -*) fail "unknown option $1; $USAGE" ;;
        *)
            # A second word is never a label: most often a room name left out of --rooms' quotes, silently dropped otherwise.
            [ -z "$label" ] || fail "unexpected argument '$1' (label already '$label'); $USAGE"
            label="$1"
            shift
            ;;
    esac
done

# The latest earlier run, before this one exists (names sort by date).
previous="$(find "$ROOT" -mindepth 1 -maxdepth 1 -type d 2>/dev/null | sort | tail -1 || true)"
[ -n "$against" ] && previous="$against"

out="$ROOT/$(date +%Y-%m-%d_%H-%M-%S)${label:+-$label}"
mkdir -p "$out"
touch "$ROOT/.gdignore" # Godot must not import the captures
{
    echo "date    $(date '+%Y-%m-%d %H:%M:%S')"
    echo "label   ${label:-none}"
    echo "world   ${world_args[1]:-prototype}"
    echo "biome   ${biome_args[1]:-room default}"
    echo "look    ${look_args[1]:-plain}"
    echo "commit  $(git rev-parse --short HEAD 2>/dev/null || echo unknown)$(git diff --quiet HEAD 2>/dev/null || echo ' + uncommitted changes')"
} >"$out/run.txt"

# godot runs the last built assembly, so build first.
dotnet build TheDeep.sln -v q -nologo >"$out/build.log" 2>&1 || {
    tail -20 "$out/build.log"
    fail "dotnet build"
}
"$GODOT" --headless --import >"$out/import.log" 2>&1 || {
    tail -20 "$out/import.log"
    fail "godot --import"
}

# The rooms rendered: the drowned hall first (perf.json, compared run to run), then every other room of the prototype's folder.
ROOMS=(DrownedHall)
for scene in worlds/prototype/rooms/*.tscn; do
    room="$(basename "$scene" .tscn)"
    [ "$room" = DrownedHall ] || ROOMS+=("$room")
done
if [ ${#world_args[@]} -gt 0 ]; then
    [ -d "worlds/${world_args[1]}/rooms" ] || fail "no world ${world_args[1]} (worlds/${world_args[1]}/rooms)"
    ROOMS=()
    for scene in "worlds/${world_args[1]}"/rooms/*.tscn; do ROOMS+=("$(basename "$scene" .tscn)"); done
fi
if [ -n "$only" ]; then read -r -a ROOMS <<<"$only"; fi
for room in "${ROOMS[@]}"; do
    name="$room"
    room_args=(--room "$room") perf="perf-$room.json" log="$out/godot-$room.log"
    if [ "$room" = "${ROOMS[0]}" ]; then
        perf="perf.json" log="$out/godot.log"
    fi
    printf '\033[1mrendering %s: photo spots, reels and frame times into %s\033[0m\n' "$name" "$out"
    "$GODOT" --path . --disable-vsync --fixed-fps 60 --resolution 960x540 -- --lookdev "$out" --size "$SIZE" --perf "$perf" \
        "${world_args[@]}" "${room_args[@]}" "${biome_args[@]}" "${look_args[@]}" >"$log" 2>&1 || {
        tail -20 "$log"
        fail "look-dev run exited non-zero (log: $log)"
    }
    if grep -E "$FAIL_PATTERN" "$log" >/dev/null; then
        grep -n -E -A3 "$FAIL_PATTERN" "$log" | head -40
        fail "look-dev run printed errors (log: $log)"
    fi
    grep -q 'LOOKDEV OK' "$log" || {
        tail -20 "$log"
        fail "look-dev run did not report LOOKDEV OK"
    }
    grep -E '^(frame|gpu|cpu) |budget' "$log"
done
# folder|Sheet|COLSxROWS|CROP (CROP optional): the reels' contact sheets (MotionReel.cs names the folders).
REELS=(
    "motion|Motion|8x7"
    "machines|Machines|6x6|1440x810"
    "grip|Grip|6x6|1440x810"
    "harpoon|Harpoon|5x3|1440x810"
    "combat|Combat|7x4|1440x810"
    "strikes|Strikes|8x7|1000x640"
    "bore|Bore|5x2|1100x800"
    "swim|Swim|7x4|900x600"
    "moments|Moments|5x2|700x700"
    "door|Door|4x2|900x1000"
    "taking|Taking|6x3|800x800"
    "pickup|Pickup|4x2|800x800"
    "lever|Lever|3x3|1800x1000"
)
for reel in "${REELS[@]}"; do
    IFS='|' read -r folder sheet grid crop <<<"$reel"
    if [ -d "$out/$folder" ]; then python3 -I tools/motion_sheet.py "$out" "$folder" "$sheet" "$grid" ${crop:+"$crop"}; fi
done
find "$out" -maxdepth 1 -name '*.png' | sort | sed 's/^/  /'

if [ -n "$previous" ] && [ -d "$previous" ]; then
    compare "$previous" "$out"
else
    echo "first run: nothing to compare with yet"
fi
