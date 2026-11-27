# Proposal

Closes #1210, a Bug under #1587.

## Why

`VectorShape.EvenOdd` is honoured by the web (`fill-rule="evenodd"`). Every Photon rasterizer dropped it and filled nonzero: CoreGraphics, Direct2D, Android and the portable one. A shape with a hole, a ring or a lake inside a region, was drawn full on Photon and open on the web. Since #674 an input shape's hit test follows the shape's own rule on both targets, so on Photon an even-odd input shape was painted over its hole and not pressable there: the press and the paint disagreed on the same screen. The `IconGlyph` that carries a shape to the rasterizer had no rule to carry.

## What Changes

- **`IconGlyph` carries the fill's rule**: `EvenOdd`, false by default, as SVG's `fill-rule` is `nonzero` by default. Its TypeScript twin and its wire shape carry it too.
- **Every Photon rasterizer fills by it**:
  - CoreGraphics with `CGContextEOFillPath`;
  - Direct2D with the alternate fill mode;
  - Android with `FillType.EvenOdd`;
  - the portable rasterizer by folding its accumulated winding (an even count paints nothing, an odd one paints).
- **A stroke ignores the rule**: it is a union.
- **A drawing's shape hands its rule to the glyph** it is rasterized from.
- **The web writes `fill-rule="evenodd"` on an icon's path** when its glyph asks for it, as it already did for a drawing's shape, in both the C# and the TypeScript lowering.

The surface changes in one place: `IconGlyph`'s constructor and `Deconstruct` take a sixth member, `EvenOdd`, and the five-member ones go.

## Impact

- `eQuantic.UI.Primitives`: `IconGlyph`.
- `eQuantic.UI.Native.Framework` (the portable rasterizer), the Apple, Windows and Android shells' rasterizers, and `EmitVisitor.Media`.
- `eQuantic.UI.Web` and the runtime's lowering, vocabulary and node shapes.
- A new spec, `vector-shapes`.
