## 1. An enum is held and combined at its width

- [x] 1.1 Measure .NET 10's answers for a 64-bit enum past 2^53, its casts (checked and not), its unboxing, its statics, and every enum operator at its width
- [x] 1.2 eqc: the width in `EnumShape`, every writer of an enum's value through it, one enumeration conversion (`EnumConversion`) and one home for the operators (`EnumOperators`)
- [x] 1.3 The runtime: `utils/enums.ts` reads a 64-bit shape as BigInts and unboxes only the underlying type; `utils/ordering.ts` orders a BigInt exactly
- [x] 1.4 The server: a 64-bit flags enum crosses as its digits, a page's top-level enum field through EqJson; hydration reads a 64-bit flags enum and its keys as BigInts
- [x] 1.5 Prove it on both sides in `EnumConformanceTests` and `HydrationCrossingTests`, failing on main

## 2. Documentation and the suites

- [x] 2.1 The wiki's SupportedFeatures page in every language it is written in, on the wiki branch of this pull request
- [x] 2.2 A ledger entry under `docs/ledger/`, citing #551 and #555
- [ ] 2.3 The suites, each read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
