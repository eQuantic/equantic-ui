- **2026-11-26 · Photon fills an even-odd shape by its rule, as the web does**: `VectorShape.EvenOdd` reached the
  web as `fill-rule="evenodd"` and no Photon rasterizer, so a ring or a lake inside a region was drawn full on Photon
  and open on the web, and an even-odd input shape was painted over a hole it could not be pressed in (#674).
  `IconGlyph` now carries the rule, and CoreGraphics, Direct2D, Android and the portable rasterizer fill by it, a
  stroke staying a union. A drawing's shape hands its rule to its glyph, and an icon's path carries `fill-rule` on the
  web from C# and from the runtime alike. `IconGlyph`'s constructor and `Deconstruct` take a sixth member, `EvenOdd`
  ([#1210](https://github.com/eQuantic/equantic-ui/issues/1210)). Proposed and archived through OpenSpec
  (`openspec/specs/vector-shapes`).
