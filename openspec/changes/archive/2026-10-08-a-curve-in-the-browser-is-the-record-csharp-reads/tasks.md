# Tasks

## 1. A curve is the record's data

- [x] 1.1 A data twin's text names each float member's kind, so a single prints its own digits; proved by `format.spec.ts` ("writes a member by the number kind the compiler names")
- [x] 1.2 `Curve` carries `[TwinIsData]` with its reason, and the export check's last exception goes; proved by `EveryValueTypeOfTheVocabulary_IsDataInTheBrowser_ExactlyWhenItSaysSo`, failing before the fix
- [x] 1.3 The design-system generator writes a preset as the record's data, each point the double that holds its single, and a motion role as a `MotionSpec`, regenerated with `EQ_UPDATE_DESIGN_TS=1`; proved by `DesignSystemTsGeneratorTests` and the vitest pins against `primitive-values.fixture.json`, which `PrimitiveValueFixtureTests` writes from the C# values
- [x] 1.4 The runtime's `CurveValue`, `MotionSpec`, `TransitionSpec` and the easing lowering read that shape; proved by the S6 cross-pins on both sides (`S6TransitionRealizerTests`, `s6-transition.spec.ts`)
- [x] 1.5 A construction builds the data and a page's state crosses a curve as its members; proved by `TwinIsDataEmissionTests.ACurve_IsBuiltAsItsData_AndPrintsItsPointsAsSingles` and `HydrationSpecEmissionTests.ARuntimeValueTypeHeldAsData_CrossesAsItsMembers`
- [x] 1.6 Run both sides through the conformance harness: `VocabularyValueConformanceTests.ACurveAnswersAsInDotNet`, failing before the fix, and the member coverage, which enumerates `Curve` now

## 2. The evaluator stays on the host

- [x] 2.1 `CurveEvaluator` is `[ServerOnly]`, and `ExtensionHome` asks the fence about the extension's home with no receiver; proved by `HostOnlyFrameworkTypeTests.AnExtensionOnAHostOnlyHome_IsNamingTheHome`, whose rows fail before the fix, `TheCurveTheEvaluatorReads_StillCrosses`, and `SharedComponentTranspilationTests.AHostOnlyHome_IsRefused_AndNamesNoImport`
- [x] 2.2 The primitives fixture drops `CurveEvaluator` by the host-only rule; proved by `PrimitivesRuntimeExportTests` and the vitest that reads the fixture

## 3. Proof and documentation

- [x] 3.1 The served runtime measured with the Server suite's budget test, and the runtime's `TestRuntime` (tsc and vitest) green
- [x] 3.2 The wiki's Styling page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 3.3 One `docs/LEDGER.md` line citing #518, and the `Curves` row of `docs/FLUTTER-PARITY.md` says what a curve is here
