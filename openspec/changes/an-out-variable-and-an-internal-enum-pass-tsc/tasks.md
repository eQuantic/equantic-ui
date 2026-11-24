## 1. An out var keeps its type (#1858)

- [x] 1.1 `EmittedArena` among the emission shapes, refused by the runtime's `tsc` on main (TS18046)
- [x] 1.2 The scanner declares an `out var` of a named type by that type, definitely assigned, where a statement writer has a context
- [x] 1.3 `OutArgument.Missed` writes a null miss as `null!` in TypeScript, and a `TryParse`'s landed read is asserted

## 2. An internal enum has its union (#1859)

- [x] 2.1 `EnumUnionsTsGenerator.UnionsIn` takes every top-level non-flags enum, internal included, and a test holds it

## 3. Documentation and the suites

- [x] 3.1 A ledger entry under `docs/ledger/`
- [x] 3.2 Every test project of the solution with the Conformance suite in full, and the runtime's `tsc` and tests, each read by its exit code
