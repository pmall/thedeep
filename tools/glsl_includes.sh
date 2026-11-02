#!/usr/bin/env bash
# Godot reimports a compute shader (*.glsl) only when that file's own text changes, not when a file it #includes
# changes. This forgets the import of every .glsl whose includes are newer than its import, so the next
# `godot --headless --import` compiles it again. Run by tools/check.sh and tools/capture.sh before they import.
set -euo pipefail
cd "$(dirname "$0")/.."

find assets -name '*.glsl' | while read -r shader; do
    base="$(basename "$shader")"
    imported="$(ls .godot/imported/"$base"-*.res 2>/dev/null | head -1 || true)"
    [ -n "$imported" ] || continue
    dir="$(dirname "$shader")"
    { grep -oE '^#include "[^"]+"' "$shader" || true; } | sed -E 's/#include "(.*)"/\1/' | while read -r inc; do
        case "$inc" in res://*) path="${inc#res://}" ;; *) path="$dir/$inc" ;; esac
        if [ -f "$path" ] && [ "$path" -nt "$imported" ]; then
            echo "glsl: $shader includes a newer $path, recompiling"
            rm -f .godot/imported/"$base"-*.md5 .godot/imported/"$base"-*.res
            break
        fi
    done
done
