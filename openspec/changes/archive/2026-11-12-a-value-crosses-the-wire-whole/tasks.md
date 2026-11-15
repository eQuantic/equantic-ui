## 1. Hydration

- [x] 1.1 Build a value through its primary constructor or its zero before adopting the payload, and keep the witness's state.
- [x] 1.2 Hydrate a dictionary's key as a value (`keyOf`).

## 2. JSON

- [x] 2.1 Write `JsonContract`, and hand its list to `$eq.json` from both emitters.

## 3. Tests

- [x] 3.1 Crossing pages for private state and for record keys, both halves executed.
- [x] 3.2 `TwinJsonConformanceTests`, against `EqJson.Options`.

## 4. Proved against the real thing

- [x] 4.1 The suites, on main 26ce35109. Verify: Web 1430 (one skipped), Server 627, Native.Engine 3391, Compiler 2946, Conformance 7891 of 7892 with one skipped, `tsc` clean and vitest 3001 of 3001; after merging main 6a5436960, the crossing and JSON conformance cases 28 of 28, the pins regenerated (the context fixture main brought writes its JSON), the docs guards and `tsc` again
- [x] 4.2 Docs. Verify: a ledger entry under `docs/ledger/`; no wiki page describes what a value carries across the wire, so none changes
