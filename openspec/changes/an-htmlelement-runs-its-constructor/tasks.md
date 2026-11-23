## 1. The constructor, read and written once

- [x] 1.1 `HtmlElementConstructorConformanceTests`: nine shapes read on both sides in both modes, eight of which diverge on main; every `Render()` a block and no field initialized where declared, clear of #1540 and #1541
- [x] 1.2 `ParsePrimitiveClass` reads constructors through `ParseConstructors`, and its own reader is gone
- [x] 1.3 `ConstructorParameter`, `ParameterStores` and `ConstructorBody`, written once and read by the primitive and the component branches

## 2. Documentation and the suites

- [x] 2.1 A ledger entry under `docs/ledger/`, and #1837's body with #1540, #1541 and the structural direction
- [x] 2.2 Every test project of the solution with the Conformance suite in full, and the runtime's `tsc` and tests, each read by its exit code
