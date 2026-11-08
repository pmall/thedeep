#!/usr/bin/env bash
# The checks (docs/testing.md#check). Fails if any step fails; each step prints how long it took.
#   tools/check.sh --fast   formats every file (applies the formatters), lints, type-checks the Python tools and builds:
#                           seconds, no Godot. Ends every turn that changed files.
#   tools/check.sh          the full check: verifies that every file is formatted and lint-clean without changing one,
#                           builds, imports headless, then runs every headless check and the unit tests at once.
# The headless checks run in parallel, one Godot each (their logs in .godot/check/); each prints its OK line when it
# passes, or its problems when it doesn't. C# layout and code style are verified by the build itself (the .editorconfig
# rules, enforced through Directory.Build.props), so the full check never runs dotnet format.
set -euo pipefail

cd "$(dirname "$0")/.."

FAST=0
case "${1:-}" in
    --fast) FAST=1 ;;
    "") ;;
    *)
        echo "usage: tools/check.sh [--fast]" >&2
        exit 2
        ;;
esac

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
GODOT="${GODOT:-godot}"
TESTS=tests/TheDeep.Tests/TheDeep.Tests.csproj

# The formatters and linters, at exact versions (docs/toolchain.md#format-tools). uvx resolves their dependencies as
# they were on UV_EXCLUDE_NEWER, so every machine runs the same code; move the date when upgrading a pin.
export UV_EXCLUDE_NEWER=2026-11-08
RUFF=(uvx ruff@0.17.4)
MYPY=(uvx --with pillow==12.1.1 mypy@2.4.1)
MDFORMAT=(uvx --from mdformat==0.7.22 --with mdformat-tables==1.0.0 mdformat --compact-tables)
SHFMT=(uvx --from shfmt-py==4.2.0 shfmt)
SHELLCHECK=(uvx --from shellcheck-py==0.11.0.1 shellcheck)
# The C# style rules dotnet format can fix but its whitespace pass leaves: braces, the file-scoped namespace, usings.
STYLE_FIXES=(IDE0011 IDE0161 IDE0065)

LOG_DIR=".godot/check"
mkdir -p "$LOG_DIR"
rm -f "$LOG_DIR"/*.log "$LOG_DIR"/*.status

# Lines that mean something went wrong in Godot output.
FAIL_PATTERN='ERROR|SCRIPT ERROR|Unhandled exception|System\.[A-Za-z.]*Exception|Failed to load|Parse Error'

START=$SECONDS
STEP_START=$SECONDS
step() {
    printf '\n\033[1m== %s\033[0m\n' "$1"
    STEP_START=$SECONDS
}
took() { printf '(%ss)\n' "$((SECONDS - STEP_START))"; }
fail() {
    printf '\033[31mCHECK FAILED: %s\033[0m\n' "$1"
    exit 1
}

# Fails if a Godot log contains an error line; prints the offending lines.
scan_log() {
    local log="$1" what="$2"
    if grep -E "$FAIL_PATTERN" "$log" >/dev/null; then
        grep -n -E -A3 "$FAIL_PATTERN" "$log" | head -40
        fail "$what printed errors (full log: $log)"
    fi
}

# The project's files of one kind, tracked or new (not ignored), one per line.
files() { git ls-files -co --exclude-standard "$@" | while read -r f; do [ -f "$f" ] && echo "$f"; done; }
mapfile -t MD_FILES < <(files '*.md')
mapfile -t JSON_FILES < <(files '*.json')
mapfile -t PY_FILES < <(files '*.py')
mapfile -t SH_FILES < <(files '*.sh')

# JSON is laid out by Python's json.tool (two-space indent): with "write", files are rewritten; without, it lists those
# that differ.
json_layout() {
    local f out bad=0
    for f in "${JSON_FILES[@]}"; do
        out="$(python3 -m json.tool --indent 2 "$f")" || return 1
        if [ "$out" != "$(cat "$f")" ]; then
            if [ "${1:-}" = write ]; then printf '%s\n' "$out" >"$f"; else
                echo "$f is not laid out by json.tool"
                bad=1
            fi
        fi
    done
    return "$bad"
}

# Builds the game and the tests; prints the compiler's output only when it fails.
build() {
    dotnet build TheDeep.sln -v q -nologo >"$LOG_DIR/build.log" 2>&1 \
        && { [ ! -f "$TESTS" ] || dotnet build "$TESTS" -v q -nologo >>"$LOG_DIR/build.log" 2>&1; }
}
build_errors() { grep -E ': (error|warning) ' "$LOG_DIR/build.log" | sed -E 's/ \[[^]]*\]$//' | sort -u | head -40; }

step "1 doc section ids and citations"
python3 tools/docrefs.py || fail "doc citations"
took

if [ "$FAST" = 1 ]; then
    step "2 format every file (dotnet format, mdformat, json.tool, Ruff, shfmt)"
    # C# layout, the game's and the core's files and the tests', at once (each run its own files).
    dotnet format whitespace TheDeep.sln >"$LOG_DIR/format-game.log" 2>&1 &
    game=$!
    dotnet format whitespace "$TESTS" --include tests/ >"$LOG_DIR/format-tests.log" 2>&1 &
    tests=$!
    "${MDFORMAT[@]}" "${MD_FILES[@]}" || fail "mdformat"
    json_layout write || fail "json.tool"
    "${RUFF[@]}" format --quiet "${PY_FILES[@]}" || fail "ruff format"
    "${SHFMT[@]}" -w "${SH_FILES[@]}" || fail "shfmt"
    wait "$game" || fail "dotnet format of the game ($LOG_DIR/format-game.log)"
    wait "$tests" || fail "dotnet format of the tests ($LOG_DIR/format-tests.log)"
    took

    step "3 lint and type-check (Ruff with its safe fixes, mypy, ShellCheck)"
    "${RUFF[@]}" check --fix --quiet "${PY_FILES[@]}" || fail "ruff check"
    "${MYPY[@]}" --strict "${PY_FILES[@]}" || fail "mypy"
    "${SHELLCHECK[@]}" "${SH_FILES[@]}" || fail "shellcheck"
    took

    step "4 build (warnings are errors, code style included)"
    if ! build; then
        if grep -qE "$(IFS='|' && echo "${STYLE_FIXES[*]}")" "$LOG_DIR/build.log"; then
            echo "fixing code style (${STYLE_FIXES[*]}) with dotnet format style"
            dotnet format style TheDeep.sln --diagnostics "${STYLE_FIXES[@]}" >/dev/null 2>&1 || true
            dotnet format style "$TESTS" --include tests/ --diagnostics "${STYLE_FIXES[@]}" >/dev/null 2>&1 || true
        fi
        build || {
            build_errors
            fail "dotnet build (full log: $LOG_DIR/build.log)"
        }
    fi
    echo "ok"
    took
    printf '\n\033[32mFAST CHECK PASSED\033[0m in %ss\n' "$((SECONDS - START))"
    exit 0
fi

step "2 formatting and lint, verified (mdformat, json.tool, Ruff, mypy, shfmt, ShellCheck)"
lint_failed=()
"${MDFORMAT[@]}" --check "${MD_FILES[@]}" || lint_failed+=(mdformat)
json_layout || lint_failed+=(json.tool)
"${RUFF[@]}" format --check --quiet "${PY_FILES[@]}" || lint_failed+=("ruff format")
"${RUFF[@]}" check --quiet "${PY_FILES[@]}" || lint_failed+=("ruff check")
"${MYPY[@]}" --strict "${PY_FILES[@]}" || lint_failed+=(mypy)
"${SHFMT[@]}" -d "${SH_FILES[@]}" || lint_failed+=(shfmt)
"${SHELLCHECK[@]}" "${SH_FILES[@]}" || lint_failed+=(shellcheck)
[ ${#lint_failed[@]} -eq 0 ] || fail "${lint_failed[*]} (tools/check.sh --fast formats and applies the safe fixes)"
echo "ok"
took

step "3 build (warnings are errors, C# layout and code style included)"
build || {
    build_errors
    fail "dotnet build (full log: $LOG_DIR/build.log; tools/check.sh --fast formats the C#)"
}
echo "ok"
took

step "4 headless import"
tools/glsl_includes.sh
"$GODOT" --headless --import >"$LOG_DIR/import.log" 2>&1 || {
    tail -20 "$LOG_DIR/import.log"
    fail "godot --import exited non-zero"
}
scan_log "$LOG_DIR/import.log" "import"
echo "ok"
took

step "5 headless checks and unit tests, in parallel"
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
    "heat|HEAT OK|HEAT CHECK|1500|--room Basalt --heat-check"
    # KeyCheck: the Sacristy key and its locked door, in place.
    "key|KEY OK|KEY CHECK|2000|--room Narthex --key-check"
    # KeyCheck's Pyre plan: the Pyre's litany (a code) and its seal on the Ashfall's low door, in place.
    "code|CODE OK|CODE CHECK|2000|--room Ashfall --code-check"
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

# The unit tests (xUnit over the core) beside them; built in step 3.
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
