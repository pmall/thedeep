#!/usr/bin/env bash
# The check step: build, headless import, smoke test, room and species checks, unit tests. Stops at the first failure.
# Run after every change and before every commit:  tools/check.sh
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
GODOT="${GODOT:-godot}"

LOG_DIR=".godot/check"
mkdir -p "$LOG_DIR"

# Lines that mean something went wrong in Godot output.
FAIL_PATTERN='ERROR|SCRIPT ERROR|Unhandled exception|System\.[A-Za-z.]*Exception|Failed to load|Parse Error'

step() { printf '\n\033[1m== %s\033[0m\n' "$1"; }
fail() { printf '\033[31mCHECK FAILED: %s\033[0m\n' "$1"; exit 1; }

# Fails if a Godot log contains an error line; prints the offending lines.
scan_log() {
    local log="$1" what="$2"
    if grep -E "$FAIL_PATTERN" "$log" >/dev/null; then
        grep -n -E -A3 "$FAIL_PATTERN" "$log" | head -40
        fail "$what printed errors (full log: $log)"
    fi
}

step "1/4 build (warnings are errors)"
dotnet build TheDeep.sln -v q -nologo || fail "dotnet build"

step "2/4 headless import"
"$GODOT" --headless --import >"$LOG_DIR/import.log" 2>&1 || { tail -20 "$LOG_DIR/import.log"; fail "godot --import exited non-zero"; }
scan_log "$LOG_DIR/import.log" "import"
echo "ok"

step "3/4 smoke test, room and species checks (main scene, headless)"
# a) Idle boot of the main scene, exactly as the owner launches it.
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 300 >"$LOG_DIR/smoke_idle.log" 2>&1 \
    || { tail -20 "$LOG_DIR/smoke_idle.log"; fail "main scene exited non-zero"; }
scan_log "$LOG_DIR/smoke_idle.log" "idle smoke run"
echo "idle boot: ok"
# b) Scripted run: SmokeDriver presses run/jump/dash and asserts the player actually moved.
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 900 -- --room DrownedHall --smoke >"$LOG_DIR/smoke_driver.log" 2>&1 \
    || { tail -20 "$LOG_DIR/smoke_driver.log"; fail "scripted smoke run exited non-zero"; }
scan_log "$LOG_DIR/smoke_driver.log" "scripted smoke run"
grep -q 'SMOKE OK' "$LOG_DIR/smoke_driver.log" || { tail -20 "$LOG_DIR/smoke_driver.log"; fail "scripted smoke run did not report SMOKE OK"; }
grep 'SMOKE OK' "$LOG_DIR/smoke_driver.log"
# c) Room scenes: grid, exits and their requirements, bounds; then the map: layout, reachability, softlocks, ability orders (RoomValidator).
# Run from the lab (no machines), so the rooms are checked alone: next to a live room with machines, the
# rooms added and freed in one frame intermittently crashed the engine (a GC finalizer fatal error).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 600 -- --room MechanicsLab --validate-rooms >"$LOG_DIR/rooms.log" 2>&1 \
    || { grep -E 'ROOM CHECK|ERROR' "$LOG_DIR/rooms.log" | head -40; fail "room check failed (full log: $LOG_DIR/rooms.log)"; }
scan_log "$LOG_DIR/rooms.log" "room check"
grep -q 'ROOMS OK' "$LOG_DIR/rooms.log" || { tail -20 "$LOG_DIR/rooms.log"; fail "room check did not report ROOMS OK"; }
grep -E 'ROOMS OK|MAP OK|INSIDE OK|DRAFT OK' "$LOG_DIR/rooms.log"
# d) Room travel: every open exit crossed, arriving in the target room and staying there (TravelValidator).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 20000 -- --validate-travel >"$LOG_DIR/travel.log" 2>&1 \
    || { grep -E 'TRAVEL CHECK|ERROR' "$LOG_DIR/travel.log" | head -40; fail "travel check failed (full log: $LOG_DIR/travel.log)"; }
scan_log "$LOG_DIR/travel.log" "travel check"
grep -q 'TRAVEL OK' "$LOG_DIR/travel.log" || { tail -20 "$LOG_DIR/travel.log"; fail "travel check did not report TRAVEL OK"; }
grep 'TRAVEL OK' "$LOG_DIR/travel.log"
# e) Mag-grip: walls, ceilings, inner and outer corners, leaping off, letting go (GripCheck, in the mechanics lab).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 4000 -- --room MechanicsLab --grip-check >"$LOG_DIR/grip.log" 2>&1 \
    || { grep -E 'GRIP CHECK|ERROR' "$LOG_DIR/grip.log" | head -40; fail "grip check failed (full log: $LOG_DIR/grip.log)"; }
scan_log "$LOG_DIR/grip.log" "grip check"
grep -q 'GRIP OK' "$LOG_DIR/grip.log" || { tail -20 "$LOG_DIR/grip.log"; fail "grip check did not report GRIP OK"; }
grep 'GRIP OK' "$LOG_DIR/grip.log"
# e2) Harpoon: anchor in sight, fire, pull, release, chain, blocked sight, range, jump cancel (HarpoonCheck, in the lab).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 4000 -- --room MechanicsLab --harpoon-check >"$LOG_DIR/harpoon.log" 2>&1 \
    || { grep -E 'HARPOON CHECK|ERROR' "$LOG_DIR/harpoon.log" | head -40; fail "harpoon check failed (full log: $LOG_DIR/harpoon.log)"; }
scan_log "$LOG_DIR/harpoon.log" "harpoon check"
grep -q 'HARPOON OK' "$LOG_DIR/harpoon.log" || { tail -20 "$LOG_DIR/harpoon.log"; fail "harpoon check did not report HARPOON OK"; }
grep 'HARPOON OK' "$LOG_DIR/harpoon.log"
# e3) Drill: a dash stopped by cracked rock without it, boring through with it, the dive through cracked rock and onto sound rock (DrillCheck, in the lab).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 2000 -- --room MechanicsLab --drill-check >"$LOG_DIR/drill.log" 2>&1 \
    || { grep -E 'DRILL CHECK|ERROR' "$LOG_DIR/drill.log" | head -40; fail "drill check failed (full log: $LOG_DIR/drill.log)"; }
scan_log "$LOG_DIR/drill.log" "drill check"
grep -q 'DRILL OK' "$LOG_DIR/drill.log" || { tail -20 "$LOG_DIR/drill.log"; fail "drill check did not report DRILL OK"; }
grep 'DRILL OK' "$LOG_DIR/drill.log"
# e4) Water and Ballast: splashing in and floating, held up without it, sinking, staying down, rising, jumping out (BallastCheck, in the lab).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 2000 -- --room MechanicsLab --ballast-check >"$LOG_DIR/ballast.log" 2>&1 \
    || { grep -E 'BALLAST CHECK|ERROR' "$LOG_DIR/ballast.log" | head -40; fail "ballast check failed (full log: $LOG_DIR/ballast.log)"; }
scan_log "$LOG_DIR/ballast.log" "ballast check"
grep -q 'BALLAST OK' "$LOG_DIR/ballast.log" || { tail -20 "$LOG_DIR/ballast.log"; fail "ballast check did not report BALLAST OK"; }
grep 'BALLAST OK' "$LOG_DIR/ballast.log"
# e5) Heat plating: burned and thrown back by a column of heat without it, through it unharmed with it (HeatCheck, in the lab).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 1000 -- --room MechanicsLab --heat-check >"$LOG_DIR/heat.log" 2>&1 \
    || { grep -E 'HEAT CHECK|ERROR' "$LOG_DIR/heat.log" | head -40; fail "heat check failed (full log: $LOG_DIR/heat.log)"; }
scan_log "$LOG_DIR/heat.log" "heat check"
grep -q 'HEAT OK' "$LOG_DIR/heat.log" || { tail -20 "$LOG_DIR/heat.log"; fail "heat check did not report HEAT OK"; }
grep 'HEAT OK' "$LOG_DIR/heat.log"
# f) Combat and session: struck, swings break a machine, death and return, rest, relic (CombatCheck, in the lab).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 4000 -- --room MechanicsLab --combat-check >"$LOG_DIR/combat.log" 2>&1 \
    || { grep -E 'COMBAT CHECK|ERROR' "$LOG_DIR/combat.log" | head -40; fail "combat check failed (full log: $LOG_DIR/combat.log)"; }
scan_log "$LOG_DIR/combat.log" "combat check"
grep -q 'COMBAT OK' "$LOG_DIR/combat.log" || { tail -20 "$LOG_DIR/combat.log"; fail "combat check did not report COMBAT OK"; }
grep 'COMBAT OK' "$LOG_DIR/combat.log"
# f2) The flooded layer's end: the boss arena's gates, the Precentor broken and remembered, the third law held then broken, the fall into Rimefall, the Choir's sleepers (EncounterCheck).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 9000 -- --room Chapterhouse --encounter-check >"$LOG_DIR/encounter.log" 2>&1 \
    || { grep -E 'ENCOUNTER CHECK|ERROR' "$LOG_DIR/encounter.log" | head -40; fail "encounter check failed (full log: $LOG_DIR/encounter.log)"; }
scan_log "$LOG_DIR/encounter.log" "encounter check"
grep -q 'ENCOUNTER OK' "$LOG_DIR/encounter.log" || { tail -20 "$LOG_DIR/encounter.log"; fail "encounter check did not report ENCOUNTER OK"; }
grep 'ENCOUNTER OK' "$LOG_DIR/encounter.log"
# f3) The whole run: the solver's shortest route from a new game to the end, played through the real exits, relics, laws, bosses and the end card (PlaythroughCheck).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 60000 -- --playthrough-check >"$LOG_DIR/playthrough.log" 2>&1 \
    || { grep -E 'PLAYTHROUGH CHECK|ERROR' "$LOG_DIR/playthrough.log" | head -40; fail "playthrough check failed (full log: $LOG_DIR/playthrough.log)"; }
scan_log "$LOG_DIR/playthrough.log" "playthrough check"
grep -q 'PLAYTHROUGH OK' "$LOG_DIR/playthrough.log" || { tail -20 "$LOG_DIR/playthrough.log"; fail "playthrough check did not report PLAYTHROUGH OK"; }
grep 'PLAYTHROUGH OK' "$LOG_DIR/playthrough.log"
# g) Machine species: data, then each one alive in the arena without and with the player (SpeciesValidator).
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 9000 -- --room MachineArena --validate-species >"$LOG_DIR/species.log" 2>&1 \
    || { grep -E 'SPECIES CHECK|ERROR' "$LOG_DIR/species.log" | head -40; fail "species check failed (full log: $LOG_DIR/species.log)"; }
scan_log "$LOG_DIR/species.log" "species check"
grep -q 'SPECIES OK' "$LOG_DIR/species.log" || { tail -20 "$LOG_DIR/species.log"; fail "species check did not report SPECIES OK"; }
grep -E '^  [a-z]|SPECIES OK' "$LOG_DIR/species.log"

step "4/4 unit tests"
if [ -f tests/TheDeep.Tests/TheDeep.Tests.csproj ]; then
    dotnet test tests/TheDeep.Tests/TheDeep.Tests.csproj -v q -nologo || fail "unit tests"
else
    echo "no test project yet"
fi

printf '\n\033[32mCHECK PASSED\033[0m\n'
