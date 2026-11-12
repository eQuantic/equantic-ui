# Tasks

## 1. The interfaces a class declares

- [x] 1.1 The class, record and component emitters write `static $interfaces`, and `$eq.types.implements` reads it. Verify: `InterfaceTypeTestConformanceTests`, executed on both sides: ten of its cases fail on main, where every test answered `x != null`, and all pass here
- [x] 1.2 A test the browser cannot answer is refused with EQ2015. Verify: the four refusal cases of the same class, and EQ2015's row in `docs/DIAGNOSTICS.md` and the wiki's Diagnostics page in five languages (`DiagnosticsDocumentedTests`, `WikiDiagnosticsTests`)
- [x] 1.3 The transpiled engines declare their markers. Verify: `SharedComponentTranspilationTests` regenerated, 13 twins gaining one line each and nothing else moving

## 2. Proved against the real thing

- [x] 2.1 The suites. Verify: Web 1429 (one skipped), Server 627 (the served runtime's budget included), Native.Engine 3391, Conformance 7834 of 7835 with one skipped, Compiler 2925 after a package's interface was kept EQ2004's, `tsc` clean and vitest 2989 of 2990, the one a table perf spec timed under load
- [x] 2.2 A ledger entry under `docs/ledger/`
