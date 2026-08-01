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

# SINGLE-ENTRY SPIR-V per stage for the Vulkan backend. Multi-entry modules are the rarity that
# breaks drivers — MoltenVK caches the SPIR-V→MSL conversion by the module BYTES plus state,
# without the entry-point name, so same-state pipelines out of one module can silently run each
# other's fragment. One entry per module is what the rest of the world ships; the explicit
# [[vk::binding]] annotations keep binding numbers stable across the split files.
mkdir -p "$OUT/spirv"
for entry in fullscreen_vertex:vertex sdf_fragment:fragment textured_fragment:fragment \
             textured_rgba_fragment:fragment layer_composite:fragment \
             blur_down:fragment blur_up:fragment; do
  name="${entry%%:*}"; stage="${entry##*:}"
  "$SLANGC" "$SRC" -target spirv -entry "$name" -stage "$stage" -o "$OUT/spirv/$name.spv"
done

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
