#!/usr/bin/env bash
# Look-dev captures: renders every photo spot of the room to PNG at 1080p, measures frame times over
# a scripted run, and compares with the previous run (before/after sheet).
#
#   tools/capture.sh [label]                 new run in lookdev/<date>_<time>[-label]/, compared with the latest run
#   tools/capture.sh [label] --against DIR   compare with DIR instead
#   tools/capture.sh [label] --biome NAME    render with resources/biomes/NAME.tres instead of each room's own
#   tools/capture.sh --compare BEFORE AFTER  only build the sheet for two existing runs
#
# Opens a small window for about half a minute (rendering needs a GPU). See docs/testing.md.
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
GODOT="${GODOT:-godot}"
ROOT="lookdev"
SIZE="1920x1080"
FAIL_PATTERN='ERROR|SCRIPT ERROR|Unhandled exception|System\.[A-Za-z.]*Exception|Failed to load|Parse Error'

fail() { printf '\033[31mCAPTURE FAILED: %s\033[0m\n' "$1"; exit 1; }

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

label="" against="" biome_args=()
while [ $# -gt 0 ]; do
    case "$1" in
        --against) against="${2%/}"; shift 2 ;;
        --biome) biome_args=(--biome "$2"); shift 2 ;;
        -*) fail "unknown option $1" ;;
        *) label="$1"; shift ;;
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
    echo "biome   ${biome_args[1]:-room default}"
    echo "commit  $(git rev-parse --short HEAD 2>/dev/null || echo unknown)$(git diff --quiet HEAD 2>/dev/null || echo ' + uncommitted changes')"
} >"$out/run.txt"

# godot runs the last built assembly, so build first.
dotnet build TheDeep.sln -v q -nologo >"$out/build.log" 2>&1 || { tail -20 "$out/build.log"; fail "dotnet build"; }

printf '\033[1mrendering photo spots and frame times into %s\033[0m\n' "$out"
"$GODOT" --path . --disable-vsync --fixed-fps 60 --resolution 960x540 -- --lookdev "$out" --size "$SIZE" "${biome_args[@]}" \
    >"$out/godot.log" 2>&1 || { tail -20 "$out/godot.log"; fail "look-dev run exited non-zero (log: $out/godot.log)"; }
if grep -E "$FAIL_PATTERN" "$out/godot.log" >/dev/null; then
    grep -n -E -A3 "$FAIL_PATTERN" "$out/godot.log" | head -40
    fail "look-dev run printed errors (log: $out/godot.log)"
fi
grep -q 'LOOKDEV OK' "$out/godot.log" || { tail -20 "$out/godot.log"; fail "look-dev run did not report LOOKDEV OK"; }
grep -E '^(frame|gpu|cpu) |budget' "$out/godot.log"
ls "$out"/*.png | sed 's/^/  /'

if [ -n "$previous" ] && [ -d "$previous" ]; then
    compare "$previous" "$out"
else
    echo "first run: nothing to compare with yet"
fi
