#!/usr/bin/env bash
# The check step: build, headless import, smoke test, unit tests. Stops at the first failure.
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

step "3/4 smoke test (main scene, headless)"
# a) Idle boot of the main scene, exactly as the owner launches it.
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 300 >"$LOG_DIR/smoke_idle.log" 2>&1 \
    || { tail -20 "$LOG_DIR/smoke_idle.log"; fail "main scene exited non-zero"; }
scan_log "$LOG_DIR/smoke_idle.log" "idle smoke run"
echo "idle boot: ok"
# b) Scripted run: SmokeDriver presses run/jump/dash and asserts the player actually moved.
if [ -f scripts/debug/SmokeDriver.cs ]; then
"$GODOT" --headless --path . --fixed-fps 60 --quit-after 900 -- --smoke >"$LOG_DIR/smoke_driver.log" 2>&1 \
    || { tail -20 "$LOG_DIR/smoke_driver.log"; fail "scripted smoke run exited non-zero"; }
scan_log "$LOG_DIR/smoke_driver.log" "scripted smoke run"
grep -q 'SMOKE OK' "$LOG_DIR/smoke_driver.log" || { tail -20 "$LOG_DIR/smoke_driver.log"; fail "scripted smoke run did not report SMOKE OK"; }
grep 'SMOKE OK' "$LOG_DIR/smoke_driver.log"
fi

step "4/4 unit tests"
if [ -f tests/TheDeep.Tests/TheDeep.Tests.csproj ]; then
    dotnet test tests/TheDeep.Tests/TheDeep.Tests.csproj -v q -nologo || fail "unit tests"
else
    echo "no test project yet"
fi

printf '\n\033[32mCHECK PASSED\033[0m\n'
