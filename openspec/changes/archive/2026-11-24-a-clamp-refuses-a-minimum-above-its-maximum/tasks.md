## 1. The clamp

- [x] 1.1 The runtime's `clamp` (a number, with its kind) and `clampLong` (a BigInt), .NET's refusal and order
- [x] 1.2 The numeric table's `Clamp` and `ClampNative` rows for every number but a decimal go to them
- [x] 1.3 `NumberClampConformanceTests`, both sides executed: each width's refusal in the invariant culture, en-US and pt-BR, NaN, negative zero, the ordinary answers and the order of evaluation; 5 of its 7 cases fail on main's compiler
- [x] 1.4 The shared components and the `bcl-surface` baseline regenerated

## 2. Proof and documentation

- [x] 2.1 The full local suites, Conformance whole, and `TestRuntime`
- [x] 2.2 The wiki's SupportedFeatures in every language, on the branch of the same name
- [x] 2.3 One ledger entry under `docs/ledger/` citing #1824
