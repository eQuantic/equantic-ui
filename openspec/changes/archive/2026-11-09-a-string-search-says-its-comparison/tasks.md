## 1. A search

- [x] 1.1 Refuse a string's search with no comparison in `StringMethodStrategy` (EQ1004), naming the fix.
- [x] 1.2 Have the suites that searched with no comparison say the one they measured.

## 2. Width and kana

- [x] 2.1 Measure the forms .NET keeps apart over the whole BMP, and spell them as a primary difference after their other form in `utils/string-statics.ts`.

## 3. Text

- [x] 3.1 Send an interface and a type parameter through the formatter, as an `object` goes (`StringConversion.HoldsAnyValue`).
- [x] 3.2 Write a length as `ToString("0.##")` writes a float, in `shared/lowering.ts`, cross-pinned by `TokenCssPxTests` and `lowering.spec.ts`.

## 4. Tests

- [x] 4.1 `WidthAndKanaConformanceTests`, with a sweep of each block against six probes, and `HeldValueTextConformanceTests` on a pt-BR page, both sides executed.

## 5. Documentation

- [x] 5.1 The wiki's SupportedFeatures page in every language, and Upgrading in the two it is written in.
- [x] 5.2 A ledger entry under `docs/ledger/`.
