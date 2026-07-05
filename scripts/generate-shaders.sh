#!/usr/bin/env bash
# Regenerates the Photon shader outputs from the ONE normative Slang source (plan D3).
# The generated files are COMMITTED (never hand-edited); this script is the only writer.
#
# Usage: EQ_SLANGC=/path/to/slangc ./scripts/generate-shaders.sh
# slangc releases: https://github.com/shader-slang/slang/releases (macos-aarch64 zip → bin/slangc).
# Packaging note: slangc will ship EMBEDDED in a NuGet package like the Bun binaries — framework
# developers run this script; app developers never see it.
set -euo pipefail

SLANGC="${EQ_SLANGC:?Set EQ_SLANGC to the slangc binary path}"
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
OUT="$ROOT/src/eQuantic.UI.Native.Engine/Shaders/Generated"

mkdir -p "$OUT"
"$SLANGC" "$SRC" -target metal -o "$OUT/Sdf.metal"
"$SLANGC" "$SRC" -target spirv -o "$OUT/Sdf.spv"
echo "Generated: $OUT/Sdf.metal ($(wc -c < "$OUT/Sdf.metal") bytes), $OUT/Sdf.spv ($(wc -c < "$OUT/Sdf.spv") bytes)"
