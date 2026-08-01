#!/usr/bin/env bash
# Regenerates the Photon shader outputs from the ONE normative Slang source (plan D3).
# The generated files are COMMITTED (never hand-edited); this script is the only writer.
#
# Usage: ./scripts/generate-shaders.sh
# The toolchain resolves itself (W2): the pinned slangc is found in the cache or acquired on first
# use, digest-verified. Set EQ_SLANGC to override with your own build.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
# shellcheck source=scripts/slang-toolchain.sh
source "$ROOT/scripts/slang-toolchain.sh"
eq_slang_resolve
SLANGC="${EQ_SLANGC:?the Slang toolchain could not be resolved}"
SRC="$ROOT/src/eQuantic.UI.Native.Engine/Shaders/Sdf.slang"
OUT="$ROOT/src/eQuantic.UI.Native.Engine/Shaders/Generated"

mkdir -p "$OUT"
"$SLANGC" "$SRC" -target metal -o "$OUT/Sdf.metal"
"$SLANGC" "$SRC" -target spirv -o "$OUT/Sdf.spv"

# Offline metallib (D3): zero runtime shader compilation on Metal. Requires the Xcode Metal
# Toolchain (xcodebuild -downloadComponent MetalToolchain); skipped with a warning if absent.
if xcrun metal --version >/dev/null 2>&1; then
  xcrun metal -c "$OUT/Sdf.metal" -o "$OUT/Sdf.air"
  xcrun metallib "$OUT/Sdf.air" -o "$OUT/Sdf.metallib"
  rm -f "$OUT/Sdf.air"
  echo "Generated: $OUT/Sdf.metallib ($(wc -c < "$OUT/Sdf.metallib") bytes)"
else
  echo "WARNING: Metal Toolchain missing — Sdf.metallib NOT regenerated (runtime MSL fallback)."
fi
echo "Generated: $OUT/Sdf.metal ($(wc -c < "$OUT/Sdf.metal") bytes), $OUT/Sdf.spv ($(wc -c < "$OUT/Sdf.spv") bytes)"
