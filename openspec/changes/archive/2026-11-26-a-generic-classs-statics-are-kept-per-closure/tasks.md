## 1. Statics per closed type (#1855)

- [x] 1.1 The runtime's `statics`, one object per closed type, with its own type initializer state, and a spec
- [x] 1.2 `ClosedStatics`: `this` in the class's own code, the closed type it names elsewhere, EQ2018 for a type argument known only at run time
- [x] 1.3 The type initializer reaches its slots through `this`, and the twin declares no type parameters, `any` aliases after the class
- [x] 1.4 `EmittedCache` and `EmittedCacheUser` among the emission shapes, refused by the runtime's `tsc` on main
- [x] 1.5 A conformance case of two closed types on both sides, failing on main

## 2. Documentation and the suites

- [x] 2.1 EQ2018 in `docs/DIAGNOSTICS.md` and the wiki, the wiki's compiler and supported-features pages, in five languages
- [x] 2.2 A ledger entry under `docs/ledger/`
- [x] 2.3 Every test project of the solution with the Conformance suite in full, and the runtime's `tsc` and tests, each read by its exit code
