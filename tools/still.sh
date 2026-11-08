#!/usr/bin/env bash
# The still check (docs/testing.md#still-check): the settle reels alone (docs/testing.md#settle-reel), in every room
# MotionReel.Settles names, at the owner's window size, painted as in play. Fails if any pixel pops while the camera
# settles, rests or creeps, or blinks while the world runs (docs/art-direction.md#stable-image). Needs the GPU to itself
# (docs/toolchain.md#gpu); opens a small window, about 15 s a room.
#
#   tools/still.sh [--rooms "A B"] [--size WxH] [-- GODOT_ARGS]   e.g. -- --paint none to measure the raw render
#
# Writes stills/<date>_<time>/ (git-ignored): settle-<Room>.json, .png and -heat.png, and each room's Godot log.
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
GODOT="${GODOT:-godot}"
# The window `godot --path .` opens (project.godot's viewport size).
SIZE="1600x900"
FAIL_PATTERN='ERROR|SCRIPT ERROR|Unhandled exception|System\.[A-Za-z.]*Exception|Failed to load|Parse Error'

fail() {
    printf '\033[31mSTILL CHECK FAILED: %s\033[0m\n' "$1"
    exit 1
}

only="" extra=() build=1
while [ $# -gt 0 ]; do
    case "$1" in
        --rooms)
            only="$2"
            shift 2
            ;;
        --size)
            SIZE="$2"
            shift 2
            ;;
        --no-build)
            build=0
            shift
            ;;
        --)
            shift
            extra=("$@")
            break
            ;;
        *) fail "unknown option $1" ;;
    esac
done

out="stills/$(date +%Y-%m-%d_%H-%M-%S)"
mkdir -p "$out"
touch stills/.gdignore # Godot must not import the frames

if [ "$build" = 1 ]; then
    # godot runs the last built assembly, and running it does not import the shaders changed since the last import.
    dotnet build TheDeep.sln -v q -nologo >"$out/build.log" 2>&1 || {
        tail -20 "$out/build.log"
        fail "dotnet build"
    }
    tools/glsl_includes.sh
    "$GODOT" --headless --import >"$out/import.log" 2>&1 || {
        tail -20 "$out/import.log"
        fail "godot --import"
    }
fi

# The rooms: those MotionReel.Settles names, in its order.
mapfile -t ROOMS < <(sed -n '/Settle\[\] Settles =/,/};/p' scripts/debug/MotionReel.cs | grep -o 'new("[A-Za-z]*"' | cut -d'"' -f2)
[ ${#ROOMS[@]} -gt 0 ] || fail "no settle reel found in scripts/debug/MotionReel.cs"
if [ -n "$only" ]; then read -r -a ROOMS <<<"$only"; fi

bad=0
for room in "${ROOMS[@]}"; do
    log="$out/godot-$room.log"
    "$GODOT" --path . --disable-vsync --fixed-fps 60 --resolution 960x540 -- --lookdev "$out" --still --size "$SIZE" \
        --room "$room" "${extra[@]}" >"$log" 2>&1 || {
        tail -20 "$log"
        fail "$room: the run exited non-zero (log: $log)"
    }
    if grep -E "$FAIL_PATTERN" "$log" >/dev/null; then
        grep -n -E -A3 "$FAIL_PATTERN" "$log" | head -40
        fail "$room: the run printed errors (log: $log)"
    fi
    line="$(grep -E '^settle ' "$log" || true)"
    [ -n "$line" ] || fail "$room: no settle line (log: $log)"
    echo "$line"
    if ! grep -qE 'pops 0 \(.*blinks 0 ' <<<"$line"; then bad=$((bad + 1)); fi
done

[ "$bad" = 0 ] || fail "$bad of ${#ROOMS[@]} rooms pop or blink: see $out/settle-<Room>-heat.png (red: pops, cyan: blinks)"
printf '\033[32mSTILL OK: no pixel pops or blinks in %s rooms (%s)\033[0m\n' "${#ROOMS[@]}" "$out"
