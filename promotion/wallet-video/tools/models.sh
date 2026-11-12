#!/usr/bin/env bash
# The device bodies the 3D scene is built from, written by slab.py into effectcraft/models/.
# Arguments: width height depth corner edge  r g b  metallic roughness  [open = no front cap].
# Units are the comp's pixels; "open" leaves the face where the screen sits uncapped, so nothing of
# the body stands in front of the screen (see the README on why that matters to the depth buffer).
set -euo pipefail
here="$(cd "$(dirname "$0")" && pwd)"
out="${1:-$here/../effectcraft/models}"
mkdir -p "$out"
slab() { python3 "$here/slab.py" "$out/$1" "${@:2}"; }

slab iphone.gltf             652 1360 70 94 14  0.55 0.56 0.58  1.0  0.28 open   # titanium
slab android.gltf            584 1244 66 66 10  0.16 0.17 0.19  0.85 0.4  open   # graphite
slab monitor.gltf           1400  960 30 26  6  0.80 0.81 0.83  1.0  0.3  open   # silver aluminium
slab monitor-neck-satin.gltf 240  560 22 20  6  0.62 0.63 0.66  0.45 0.4         # satin, or it renders black
slab monitor-foot.gltf       560  400 14 40  6  0.80 0.81 0.83  1.0  0.3
slab laptop-lid.gltf        1300  860 12 28  5  0.30 0.31 0.33  1.0  0.32 open   # space grey
slab laptop-base.gltf       1300  880 18 34  8  0.30 0.31 0.33  1.0  0.32
