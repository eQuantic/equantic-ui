## 1. A collection's negative count

- [x] 1.1 Measure .NET 10's throw for each negative count, and which go through the guard
- [x] 1.2 `throwIfNegativeCount` in `utils/guards.ts`, and the two private copies gone
- [x] 1.3 `ArgumentGuardConformanceTests.ACollectionsNegativeCount_IsRefusedWithItsValue`, en-US and sv-SE, both sides

## 2. An unnamed enum value

- [x] 2.1 `HydrationSpec` marks a non-flags enum, as a value and as a key
- [x] 2.2 `utils/hydrate.ts` revives an unnamed value's digits
- [x] 2.3 `HydrationCrossingTests.AnUnnamedValueOfAnEnum_CrossesAsTheBrowserHoldsIt` and the spec pins

## 3. Documentation and the suites

- [x] 3.1 A ledger entry under `docs/ledger/`
- [ ] 3.2 The suites, each read by its exit code: Compiler, Web, Server, Conformance, and the runtime's `TestRuntime`
