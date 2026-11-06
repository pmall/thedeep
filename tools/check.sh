#!/usr/bin/env bash
# The check step: doc citations, build, headless import, then every headless check and the unit tests at once. Fails if any fails.
# Run after every change and before every commit:  tools/check.sh
# Each step prints how long it took. The headless checks run in parallel, one Godot each (their logs in .godot/check/); each
# prints its OK line when it passes, or its problems when it doesn't.
set -euo pipefail

cd "$(dirname "$0")/.."

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
GODOT="${GODOT:-godot}"
TESTS=tests/TheDeep.Tests/TheDeep.Tests.csproj

LOG_DIR=".godot/check"
mkdir -p "$LOG_DIR"
rm -f "$LOG_DIR"/*.log "$LOG_DIR"/*.status

# Lines that mean something went wrong in Godot output.
FAIL_PATTERN='ERROR|SCRIPT ERROR|Unhandled exception|System\.[A-Za-z.]*Exception|Failed to load|Parse Error'

START=$SECONDS
STEP_START=$SECONDS
step() { printf '\n\033[1m== %s\033[0m\n' "$1"; STEP_START=$SECONDS; }
took() { printf '(%ss)\n' "$((SECONDS - STEP_START))"; }
fail() { printf '\033[31mCHECK FAILED: %s\033[0m\n' "$1"; exit 1; }

# Fails if a Godot log contains an error line; prints the offending lines.
scan_log() {
    local log="$1" what="$2"
    if grep -E "$FAIL_PATTERN" "$log" >/dev/null; then
        grep -n -E -A3 "$FAIL_PATTERN" "$log" | head -40
        fail "$what printed errors (full log: $log)"
    fi
}

step "1/4 doc section ids and citations"
python3 tools/docrefs.py || fail "doc citations"
took

step "2/4 build (warnings are errors)"
dotnet build TheDeep.sln -v q -nologo || fail "dotnet build"
if [ -f "$TESTS" ]; then dotnet build "$TESTS" -v q -nologo || fail "dotnet build of the tests"; fi
took

step "3/4 headless import"
tools/glsl_includes.sh
"$GODOT" --headless --import >"$LOG_DIR/import.log" 2>&1 || { tail -20 "$LOG_DIR/import.log"; fail "godot --import exited non-zero"; }
scan_log "$LOG_DIR/import.log" "import"
echo "ok"
took

step "4/4 headless checks and unit tests, in parallel"
trap 'kill $(jobs -p) 2>/dev/null' INT TERM

# The checks: name | what its passing line starts with | what its problem lines start with | frames before Godot quits | arguments.
# The driver's docblock says what each one proves (docs/testing.md#check).
CHECKS=(
    # The main scene's idle boot, exactly as the owner launches it (no driver: passes when it runs clean).
    "smoke_idle||ERROR|300|"
    # SmokeDriver presses run, jump and dash and asserts the player moved.
    "smoke_driver|SMOKE OK|SMOKE|900|--room DrownedHall --smoke"
    # RoomValidator: every room scene, then the map. Run from the lab (no machines), so the rooms are checked alone: next to a
    # live room with machines, rooms added and freed in one frame intermittently crashed the engine (a GC finalizer fatal error).
    "rooms|ROOMS OK|ROOM CHECK|600|--room MechanicsLab --validate-rooms"
    # TravelValidator: every open exit crossed along its opening and back, in four parts at once (the longest check).
    "travel1|TRAVEL OK|TRAVEL CHECK|20000|--validate-travel --shard 1/4"
    "travel2|TRAVEL OK|TRAVEL CHECK|20000|--validate-travel --shard 2/4"
    "travel3|TRAVEL OK|TRAVEL CHECK|20000|--validate-travel --shard 3/4"
    "travel4|TRAVEL OK|TRAVEL CHECK|20000|--validate-travel --shard 4/4"
    # The abilities through the real inputs, in the mechanics lab.
    "grip|GRIP OK|GRIP CHECK|4000|--room MechanicsLab --grip-check"
    "harpoon|HARPOON OK|HARPOON CHECK|4000|--room MechanicsLab --harpoon-check"
    "drill|DRILL OK|DRILL CHECK|2000|--room MechanicsLab --drill-check"
    "ballast|BALLAST OK|BALLAST CHECK|2000|--room MechanicsLab --ballast-check"
    "heat|HEAT OK|HEAT CHECK|1000|--room MechanicsLab --heat-check"
    # KeyCheck: the Sacristy key and its locked door, in place.
    "key|KEY OK|KEY CHECK|2000|--room Narthex --key-check"
    # SecretCheck: the Sacristy's hidden passage into the Cell.
    "secret|SECRET OK|SECRET CHECK|1500|--room Sacristy --secret-check"
    # AmbushCheck: the Pyre's trap.
    "ambush|AMBUSH OK|AMBUSH CHECK|1500|--room Pyre --ambush-check"
    # DialogueCheck: the Lamplighter's conversation, in the Narthex.
    "dialogue|DIALOGUE OK|DIALOGUE CHECK|1500|--room Narthex --dialogue-check"
    # PowerUpCheck: power-ups taken, in the Sacristy.
    "powerup|POWERUP OK|POWERUP CHECK|1500|--room Sacristy --powerup-check"
    # FightCheck: the fight moveset on the lab's Votary.
    "fight|FIGHT OK|FIGHT CHECK|1500|--room MechanicsLab --fight-check"
    # CombatCheck, EncounterCheck, PlaythroughCheck, SpeciesValidator.
    "combat|COMBAT OK|COMBAT CHECK|4000|--room MechanicsLab --combat-check"
    "encounter|ENCOUNTER OK|ENCOUNTER CHECK|9000|--room Chapterhouse --encounter-check"
    "playthrough|PLAYTHROUGH OK|PLAYTHROUGH CHECK|60000|--playthrough-check"
    "species|SPECIES OK|SPECIES CHECK|9000|--room MachineArena --validate-species"
    # The sequel world (docs/roadmap.md#sequel-test), a fixture: its rooms and map (run from the Strongroom, its room without
    # machines, as the prototype's are from the lab), every exit crossed, and the whole run.
    "sequel_rooms|ROOMS OK|ROOM CHECK|600|--world sequel --room Strongroom --validate-rooms"
    "sequel_travel|TRAVEL OK|TRAVEL CHECK|20000|--world sequel --validate-travel"
    "sequel_playthrough|PLAYTHROUGH OK|PLAYTHROUGH CHECK|20000|--world sequel --playthrough-check"
)

# Runs one check in the background: its log, then a status file with its exit code and how long it took.
launch() {
    local name="$1" frames="$2" args="$3" log="$LOG_DIR/$1.log"
    (
        local t0=$SECONDS code=0
        # shellcheck disable=SC2086 # the arguments are split on purpose
        if [ -n "$args" ]; then
            "$GODOT" --headless --path . --fixed-fps 60 --quit-after "$frames" -- $args >"$log" 2>&1 || code=$?
        else
            "$GODOT" --headless --path . --fixed-fps 60 --quit-after "$frames" >"$log" 2>&1 || code=$?
        fi
        echo "$code $((SECONDS - t0))" >"$LOG_DIR/$name.status"
    ) &
}

for entry in "${CHECKS[@]}"; do
    IFS='|' read -r name ok problem frames args <<<"$entry"
    launch "$name" "$frames" "$args"
done

# The unit tests (xUnit over the core) beside them; built in step 2.
if [ -f "$TESTS" ]; then
    (
        t0=$SECONDS code=0
        dotnet test "$TESTS" --no-build -v q -nologo >"$LOG_DIR/unit.log" 2>&1 || code=$?
        echo "$code $((SECONDS - t0))" >"$LOG_DIR/unit.status"
    ) &
fi

wait

failed=()
for entry in "${CHECKS[@]}"; do
    IFS='|' read -r name ok problem frames args <<<"$entry"
    log="$LOG_DIR/$name.log"
    read -r code secs <"$LOG_DIR/$name.status"
    if [ "$code" != 0 ] || grep -E "$FAIL_PATTERN" "$log" >/dev/null || { [ -n "$ok" ] && ! grep -q "$ok" "$log"; }; then
        printf '\033[31m%s FAILED\033[0m (exit %s, %ss; full log: %s)\n' "$name" "$code" "$secs" "$log"
        { grep -n -E -A3 "$problem|$FAIL_PATTERN" "$log" || tail -20 "$log"; } | head -40
        failed+=("$name")
        continue
    fi
    if [ -n "$ok" ]; then
        grep -E "^$ok|^(MAP|INSIDE|DRAFT) OK" "$log" | sed "s/\$/  ($name, ${secs}s)/" | head -6
    else
        echo "$name: ok (${secs}s)"
    fi
done

if [ -f "$TESTS" ]; then
    read -r code secs <"$LOG_DIR/unit.status"
    if [ "$code" != 0 ]; then
        tail -40 "$LOG_DIR/unit.log"
        failed+=("unit tests")
    else
        grep -E 'Passed!|Failed!' "$LOG_DIR/unit.log" | sed "s/\$/  (unit tests, ${secs}s)/"
    fi
fi
took

[ ${#failed[@]} -eq 0 ] || fail "${failed[*]}"
printf '\n\033[32mCHECK PASSED\033[0m in %ss\n' "$((SECONDS - START))"
