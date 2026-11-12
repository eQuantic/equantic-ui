## 1. An array of an enum is typed as the enum crosses

- [x] 1.1 `DeclaredType` swaps an enum inside an array's element, at any depth, as inside a generic's arguments. Verify: `AnArrayOfAnEnumTests` (a static and an instance field of `TypeRole[]`, a `TypeRole[][]`, an app enum's `Shade[]`, and the union imported from the runtime) fails 5 of 5 on main and passes here
- [x] 1.2 A component's `ColorToken` field is pinned. Verify: `AComponentsColorTokenFieldTests` reads `_ink!: ColorToken;` and `static shared: ColorToken;`, never `= undefined`

## 2. Proved against the real thing

- [x] 2.1 The transpiled libraries' pins. Verify: `SharedComponentTranspilationTests` and `PrimitivesRuntimeExportTests` pass, 15 of 15
- [x] 2.2 Docs. Verify: a ledger entry cites #1064 and #1065; no wiki page describes a twin's annotations
