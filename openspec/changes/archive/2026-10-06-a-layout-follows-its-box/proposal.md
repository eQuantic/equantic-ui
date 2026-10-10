# Proposal

Closes #636, a sub-issue of #190 (Flutter parity: the gaps that are work). Met by falei.pt on
0.2.0-preview.60: a district map at the width of its column, a tooltip over the hovered district,
and room cards in an auto-filling grid.

## Why

A handoff lays a picture out the way CSS does, against the box it lands in: a map at
`width: 100%` keeping the artwork's own aspect, a tooltip at `left: 42%; top: 30%` pulled back by
half its own width, and cards in `repeat(auto-fill, minmax(210px, 1fr))`. The vocabulary measures
all three in points. An app picks a number per window class and guesses, renders every card once
per `AdaptiveNode` arm, and cannot centre anything on its own anchor.

## What Changes

- **A drawing fills its width.** `Drawing`'s width is a `SizeValue`: `Drawing(art, width: 240)`
  still works through the implicit conversion, and `Drawing(art, SizeValue.Fill)` fills its
  parent's width with the artwork's own aspect deciding the height. An explicit height still wins.
  The web lowers it to `width: 100%` and `aspect-ratio`; Photon resolves the width like a canvas's
  and paints at the laid-out box.
- **A positioned child is placed by fraction.** `Positioned` gains `StartFraction`, `TopFraction`,
  `EndFraction` and `BottomFraction`, each a fraction of its stack (0.42 is 42%), and `ShiftX` and
  `ShiftY`, a translation in fractions of the child's OWN size applied after placement (Flutter's
  `FractionalTranslation`): `-0.5, -1` puts the child's bottom centre on the anchor. A point offset
  and a fraction on the same edge add up. The web lowers them to percentages and a `translate` on
  the anchor; Photon places by the same arithmetic, so the hit region follows the child.
- **A grid repeats a track as often as fits.** `GridTrack.AutoFill(min, weight = 1)` repeats one
  track as often as tracks of at least `min` fit, and shares what is left between them by weight.
  It is the whole column list, alone: a grid that combines it with other tracks is refused at
  construction. The web lowers it to `repeat(auto-fill, minmax(min, weight fr))`; Photon counts the
  tracks from the width it is given.

For a developer using the SDK: three layouts that took a number per window class take the box
instead, and the same C# lays out the same on the web and on Photon.

## Capabilities

### New Capabilities

- `layout`: how vocabulary nodes size and place themselves against the box they are given.

### Modified Capabilities

None.

## Impact

- **Public surface moves.** `Drawing`'s constructor takes `SizeValue width` and `Drawing.Width` is
  a `SizeValue` (its `Height` is the explicit height, 0 when derived); the `UI.Drawing` factory
  follows. `Positioned` gains six init properties and the `UI.Positioned` factory a matching tail.
  `GridTrack` gains `Min` and `Repeats` and the `AutoFill` factory. A break in preview: code that
  read `Drawing.Width` as a `float` reads `drawing.Width.Value`; nothing else changes for a caller.
- **Parts reached**: Primitives (the three nodes), Components (the factories), the web realizer
  (C#) and its TypeScript twin (the vocabulary mirror, the wire shapes and the lowering), the
  Photon measure pass and the drawing's paint. eqc, the templates and the SDKs are untouched. The
  developer surface does not move.
