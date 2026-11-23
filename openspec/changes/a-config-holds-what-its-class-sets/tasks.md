## 1. The configs hold what C# sets

- [x] 1.1 `VocabularyConfigShapeTests` reads every config a hand-written twin takes, named or written in place, against its C# class both ways, with a baseline that may only shrink; on main's mirror it fails with 179 offenders
- [x] 1.2 `BoxStyleConfig` gains `foreground`, and the test fails on a revert of that line alone
- [x] 1.3 A `VisualNodeConfig` the eight named node configs extend and the nineteen in-place configs intersect, and `Spacer`'s config gains `fixedLength`; the test fails on a revert of one intersection alone
- [x] 1.4 A member the constructor also takes is no exception: a `SingleChildNodeConfig`, and each such member in its config
- [x] 1.5 `EmittedConfigs` among the emission shapes eqc transpiles into the runtime's fixtures, refused eight times by the runtime's `tsc` on main's mirror, and run by `vocabulary-config.spec.ts`

## 2. Documentation and the suites

- [x] 2.1 A ledger entry under `docs/ledger/`
- [x] 2.2 The runtime's `tsc` and tests, and every test project of the solution with the Conformance suite in full, each read by its exit code
