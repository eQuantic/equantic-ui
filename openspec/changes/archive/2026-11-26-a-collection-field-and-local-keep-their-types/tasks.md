## 1. A field and a local keep their types (#1866)

- [x] 1.1 `EmittedWaiting` among the emission shapes, refused by the runtime's `tsc` on main (TS2304, TS2322)
- [x] 1.2 A field of every runtime collection class is declared `any` (`TypeScriptEmitter.IsRuntimeCollectionName`)
- [x] 1.3 The factories carry their type arguments (`TsNamedType.TypeArguments`), for `new` and for a collection expression
- [x] 1.4 The compiler's tests that convert with type annotations pin a factory's text with its type arguments
- [x] 1.5 None without type annotations (JavaScript), and `any` for a kept-out type, a free position (no EQ2010)

## 2. Documentation and the suites

- [x] 2.1 A ledger entry under `docs/ledger/`
- [x] 2.2 Every test project of the solution with the Conformance suite in full, and the runtime's `tsc` and tests, each read by its exit code
