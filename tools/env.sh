# shellcheck shell=bash disable=SC2034
# Sourced by tools/check.sh, tools/capture.sh and tools/map.sh: the toolchain environment (instructions/toolchain.md) and
# FAIL_PATTERN, the lines that mean something went wrong in Godot output.
export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$HOME/.local/bin:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
FAIL_PATTERN='ERROR|SCRIPT ERROR|Unhandled exception|System\.[A-Za-z.]*Exception|Failed to load|Parse Error'
