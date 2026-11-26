# Tasks

## 1. The rule

- [x] 1.1 `IconGlyph` carries `EvenOdd`, and its TypeScript twin and wire shape carry it
- [x] 1.2 CoreGraphics, Direct2D, Android and the portable rasterizer fill by it; a stroke ignores it
- [x] 1.3 A drawing's shape hands its rule to its glyph
- [x] 1.4 The web writes `fill-rule="evenodd"` on an icon's path, in C# and in the runtime

## 2. Proof

- [x] 2.1 The platform rasterizer (CoreGraphics here) leaves an even-odd ring's centre empty and fills it at nonzero; fails without the rule
- [x] 2.2 The portable rasterizer does the same, and a stroke stays a union; fails without the rule
- [x] 2.3 A drawing's even-odd shape reaches the rasterizer with its rule; fails without it
- [x] 2.4 The component parity fixture holds `icon-even-odd` on both sides; the runtime's side fails without its lowering
- [x] 2.5 The four shells build; the engine, web, server, conformance, testing and runtime suites pass

## 3. Documentation

- [x] 3.1 One entry under `docs/ledger/`
