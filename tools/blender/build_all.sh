#!/bin/sh
# Rebuilds every character model. Run from the repository root (Git Bash).
BLENDER="${BLENDER:-/c/Program Files/Blender Foundation/Blender 5.3/blender.exe}"
ROOT="$(pwd -W 2>/dev/null || pwd)"
for who in erik jonte maja linn male female; do
  "$BLENDER" --background --disable-autoexec --factory-startup "tools/blender/_downloads/human-base-meshes/human_base_meshes_bundle.blend" \
    --python tools/blender/build_hero.py -- "$who" "$ROOT/SkiGame/Assets/_Game/Resources/Models" "$ROOT/SkiGame/Screenshots/blender" 2>&1 \
    | grep -E "^(HEIGHT|EXPORT|Traceback|  File|.*Error)"
done
