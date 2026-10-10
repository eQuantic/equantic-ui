# Tasks

## 1. A drawing fills its width

- [x] 1.1 `Drawing` takes a `SizeValue` width; `Height` is the explicit height (0 when derived); the factory follows
- [x] 1.2 The web realizer (C#) and the TypeScript twin lower a fill width to `width: 100%` and `aspect-ratio`, byte-identical
- [x] 1.3 Photon measures it like a canvas and derives the height from the aspect; the paint rasterizes at the laid-out box
- [x] 1.4 Pinned on both sides of the parity line and in the native suite (falei.pt's map stays in points: its hit-testing canvas is sized in points, a follow-up of its own)

## 2. A positioned child is placed by fraction

- [x] 2.1 `Positioned` gains the four fraction offsets and the two shifts; the factory gains the tail
- [x] 2.2 The web realizer and its twin lower them to percentages, `calc()` with a point offset, and a `translate` on the anchor, byte-identical
- [x] 2.3 Photon places by the same arithmetic in the stack's placement pass
- [x] 2.4 Pinned on both sides and natively; proven on falei.pt's district tooltip in a browser (left at the label fraction, `translate(-50%, -100%)`, its foot 16 above the district)

## 3. A grid repeats a track as often as fits

- [x] 3.1 `GridTrack` gains `Min` and `Repeats` and `AutoFill(min, weight)`; `Grid` refuses it beside another track
- [x] 3.2 The web realizer and its twin lower it to `repeat(auto-fill, minmax(…))`, byte-identical
- [x] 3.3 Photon counts the tracks from its width before sizing them
- [x] 3.4 Pinned on both sides and natively; proven on falei.pt's room cards in a browser (three columns at 820, one at 343, no horizontal scroll)

## 4. Documentation and the suites

- [x] 4.1 The wiki, English and Portuguese, on this pull request's wiki branch: Icons (the drawing), Components (Positioned and the auto-fill track), Upgrading (the `Drawing.Width` break)
- [x] 4.2 `docs/FLUTTER-PARITY.md` rows; `docs/LEDGER.md` one line citing #636
- [x] 4.3 PublicAPI updated; the full suites, each alone and read by its exit code: Web, Native.Engine, Design, Compiler, Conformance, Server, Email, Images, and the runtime's 2,112 specs
