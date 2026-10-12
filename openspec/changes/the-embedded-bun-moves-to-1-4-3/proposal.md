# Proposal

Closes #854, a Task under #160 (Instruments that fail, not warn).

## Why

The six platform runtime packages embed bun 1.3.14, and bun 1.4.3 was published on 2026-10-10. The
embedded bun bundles every app's modules and the runtime the Server serves, and the conformance suite
executes every case in it, so the SDK's answers are measured in the engine a developer's build runs.

The move is not only bytes. `Bun.stringWidth` is the oracle the code engine's width table is held to
(`CellWidthOracleTests`), and bun 1.4.3 follows Unicode 17 where 1.3.14 followed 15.1: 616 characters
changed their answer, and the table said the old one for each.

## What Changes

- The six archives hold bun 1.4.3, each the same build it replaces: darwin x64 and aarch64, linux
  x64-baseline and aarch64, windows x64-baseline and aarch64. They come from the release, checked
  against its `SHASUMS256.txt`, and are repacked as the packages hold them. `bun-toolchain.json` names
  the version and the six digests.
- The code engine's cell widths follow Unicode 17, in .NET and in the browser. Two cells now: the
  Yijing and Tai Xuan Jing symbols, the counting rods, the Tangut and Khitan characters and the emoji
  Unicode 16 and 17 added. No cell now: the directional embeddings, overrides and isolates, the Arabic
  letter mark, the Mongolian vowel separator, and a Hangul vowel or final, which a font draws over the
  consonant before it.
- The pin's regeneration (`EQ_UPDATE_BUN_MANIFEST=1`) wrote a digest that starts with a digit without
  its line's key. It keeps the line now, and has a case of its own.

For a developer: nothing to write. A shared chunk gets a new name, since bun names it by a hash that
changed with the version, and a line of code that holds one of those characters is laid out as fonts
draw it.

It reaches the six runtime packages, the code engine (`eQuantic.UI.Code`) and its twin in the runtime.
The public surface and the developer surface do not move.

## Capabilities

### New Capabilities

- `code-cells`: how many cells of the monospace grid a character of a line of code takes.

### Modified Capabilities

None.
