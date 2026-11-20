One engineer, in this order. The four live in one assembly and every call site of a component moves in
the commit that converts it, so the tree compiles at every commit; the areas do not compile apart. Every
build and test runs through `python3 scripts/one-build-at-a-time.py -- …`, one test project or filter at
a time, with the `EQ_WIKI_DIR` of the delivery.

## 1. The equality the design leans on, executed on both sides

- [ ] 1.1 Add a conformance case to `tests/eQuantic.UI.Conformance.Tests` (beside
      `AnAppsEqualityConformanceTests`): a generic class holding `IReadOnlyList<Choice<T>>` (a record declared in
      the case's source) that answers the first index whose value `EqualityComparer<T>.Default.Equals` the given
      one, over string (ordinal, null), enum, int, long, Guid (parsed upper, looked up lower), record, `Level?`
      with a null entry, decimal (`1.0m` vs `1.00m`), DateTime, NaN, a class without `Equals` (the same instance
      and another), a tuple, and two entries with one value. The design's scratch ran exactly these nine
      cases and passed (the report has them)
- [ ] 1.2 Check: the case passes on both sides; make `sameGeneric` compare by reference and see the record,
      Guid and decimal rows fail (docs/PROVING.md: the test fails with the plausible wrong one), then restore it

## 2. eqc writes a generic twin the runtime's `tsc` accepts

- [ ] 2.1 `src/eQuantic.UI.Compiler/CodeGen/RecordTypeEmitter.cs`: a generic record's twin declares the type
      parameters its text writes (`class Choice<T>` for `declare value: T`), so a reference with type arguments
      (`Choice<T>[]` in a factory's signature) names a generic class
- [ ] 2.2 `src/eQuantic.UI.Compiler/CodeGen/TypeScriptEmitter.cs`: a type parameter is never collected as an
      import (`import { T } from "./T"`), and a class header declares only the type parameters its members'
      annotations use (the component twin's members are `any`, so its `<T>` is TS6133 today)
- [ ] 2.3 Compiler tests pinning both texts (in `tests/eQuantic.UI.Compiler.Tests`, beside
      `EmittedTypeSurfaceTests`), each failing on today's emitter; the component pins regenerated with
      `EQ_UPDATE_TRANSPILED=1` move by nothing (no generic type in the library yet)
- [ ] 2.4 Check against the real thing: the `transpiler-classes` scenarios, by running task 4's
      `TestRuntime` with this fix and seeing it fail without it (`Choice.ts` TS2304, `UI.ts` TS2315, `Tabs.ts`
      TS2307 and TS6133 were the scratch's errors); the JavaScript of the conformance suite's generic cases is
      unchanged

## 3. The guards learn a generic component before one exists

- [ ] 3.1 `tests/eQuantic.UI.Web.Tests/FactoryArguments.cs`: one place that closes a generic type definition
      and a generic method definition over `string` (`MakeGenericType`, `MakeGenericMethod`), used by every
      caller that builds or calls a component or a factory
- [ ] 3.2 `tests/eQuantic.UI.Native.Engine.Tests/ComponentCatalog.cs`: `Derive` closes a generic component
      instead of filtering `ContainsGenericParameters`, and fails naming one it cannot close (a constraint
      `string` does not meet); `VariantMatrixTests.Control.Name` and the parity fixture name a closed type by
      its definition without the arity, so `Components.SegmentedControl.txt` keeps its name
- [ ] 3.3 `UiFactoryConformanceTests`: a factory's name is compared with its type's name without the arity
      (the scratch failed exactly here: "Expected factory.Name to be "ScratchTabs`1""), and a generic factory
      is closed before it is called; `AdoptConfigCoverageTests` builds a generic component closed
- [ ] 3.4 Check, once task 4 lands: the variant matrix, the semantics parity fixture, `AdoptConfigCoverageTests`
      and `UiFactoryConformanceTests` each name `Tabs` among what they measured (assert it in each, so a generic
      component that drops out fails rather than passes)

## 4. `Choice<T>` and `Tabs<T>`, the first generic component, end to end

- [ ] 4.1 `src/eQuantic.UI.Components/Choice.cs`: `public sealed record Choice<T>(T Value, string Label)`, its
      XML doc saying what `Label` is (the name drawn when no builder draws one, and the accessible name)
- [ ] 4.2 `Tabs.cs` → `Tabs<T>(IReadOnlyList<Choice<T>> items, T value, Action<T>? onChanged = null)`, its
      property `Items` (a member named `Tabs` inside `Tabs<T>` is CS0542, measured):
      `selected = IndexOf(value)` by `EqualityComparer<T>.Default`, first match; `onChanged(choice.Value)` on a
      press; the `Adjustable` steps by position and hands a value, from no selection to the first entry
      forward and the last backward
- [ ] 4.3 `UI.cs`: `Tabs<T>(…)` mirroring the constructor
- [ ] 4.4 Every call site of `Tabs`: the tests under `tests/eQuantic.UI.Web.Tests` and
      `tests/eQuantic.UI.Native.Engine.Tests` (`rg -P '(?<![.\w])(new )?Tabs\(' tests`), `wave2-components.spec.ts`
      and `component-parity.spec.ts` in the runtime, and the samples `PaymentsPage.cs`, `WalletApp.cs`,
      `Gallery.cs`, each with a value of its own (an enum where the sample filters by one) replacing its `int`
- [ ] 4.5 Regenerate the twins (`EQ_UPDATE_TRANSPILED=1` on `SharedComponentTranspilationTests`): `Tabs.ts`,
      `Choice.ts`, `UI.ts` and the barrel; read the diff: the class is `Tabs`, its `$typeId` is `…Tabs\`1`, and
      the lookup calls the runtime's generic equality
- [ ] 4.6 Check, on both targets: a tab press and a forward step from the last tab hand values on the web
      (`ComponentTester`, and vitest driving keydown in `wave2-components.spec.ts`) and on Photon (`PhotonHost`,
      `KeyboardTests`); `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime` passes; `dotnet build
      samples/DefaultUIDashboard` and `samples/WalletMobile` compile through eqc (an app calling a generic
      factory); the variant matrix pins of `Tabs` do not move

## 5. `SegmentedControl<T>`, `RadioGroup<T>` and `CultureSwitcher`

- [ ] 5.1 `SegmentedControl<T>(IReadOnlyList<Choice<T>> segments, T value, Action<T>? onChanged = null)` and its
      factory with the `stretch` tail, as decision 5 says
- [ ] 5.2 `RadioGroup<T>(IReadOnlyList<Choice<T>> options, T value, Action<T>? onChanged = null, string? label = null)`
      and a new factory `UI.RadioGroup<T>` mirroring it
- [ ] 5.3 `CultureSwitcher`: a `SegmentedControl<string>` over `Choice(option.Name, option.Label)`, its value the
      active option's name, `Switch` taking the name; its `Menu` form unchanged
- [ ] 5.4 Every call site of both, in the tests, the runtime's specs and the samples (`PaymentsPage`,
      `MarkdownScreen`, `WalletApp`, `StudioShell`, `Gallery`): `StudioShell`'s theme switch a
      `SegmentedControl<ThemeMode>` and its size a `SegmentedControl<SizeVariant>`, `PaymentsPage`'s range a
      `SegmentedControl<DateRange>`
- [ ] 5.5 Regenerate the twins; check on both targets: a segment press and a radio press hand their values, a
      value no entry carries draws no thumb and no checked radio and its steps land on the first and the last
      entry, `CultureSwitcher` still lights `pt` for `pt-BR` and switches by name (its existing tests, run on
      both targets); the variant matrix pins of `SegmentedControl` and `RadioGroup` do not move

## 6. `Select<T>` and `OptionGroup<T>`

- [ ] 6.1 `OptionGroup<T>(string Label, IReadOnlyList<Choice<T>> Options)`
- [ ] 6.2 `Select<T>(IReadOnlyList<Choice<T>> options, T value, Action<T>? onChanged = null, string? placeholder = null, IReadOnlyList<OptionGroup<T>>? groups = null)`:
      `_choices` holds `Choice<T>`; the field shows the selected entry's `Label` or the placeholder; the
      highlight opens on the selected entry or the first; `Choose` hands the entry's value; `AdoptConfig`
      tests `next is not Select<T>` and copies `Value` where it copied `SelectedIndex`
      (`AdoptConfigCoverageTests` holds it)
- [ ] 6.3 `UI.Select<T>` mirroring it; every call site, `select-groups.spec.ts`, `component-parity.spec.ts`,
      `tests/eQuantic.UI.Testing.Tests/ComponentTesterTests.cs`, `PaymentsPage` and `Gallery` included
- [ ] 6.4 Regenerate the twins and the parity fixture (`EQ_UPDATE_PARITY_FIXTURE=1`), and read the diff: only
      the constructions moved
- [ ] 6.5 Check, on both targets, the `anchored-panels` and `screen-reader-parity` scenarios as this change
      rewrites them: the arrows cross a group and Enter hands `"bra"` in the browser (vitest drives keydown) and
      on Photon; an empty `Select<DateRange?>` shows its placeholder and Enter hands `DateRange.Week`

## 7. The surface, the baselines and the fixtures

- [ ] 7.1 `./scripts/public-api.sh update`: `PublicAPI.Unshipped.feat-a-choice-answers-the-apps-own-value.txt` in
      `src/eQuantic.UI.Components` holds the `*REMOVED*` lines of the four, of `OptionGroup` and of the three
      factories, and the generic surface; read the diff (it IS the API review). The developer surface
      (`developer-surface.baseline.txt`) does not move: `DeveloperSurfaceContractTests` passes unregenerated
- [ ] 7.2 `tests/eQuantic.UI.Web.Tests/Coverage/catalog-customization/`: `Tabs.Labels`,
      `SegmentedControl.Segments`, `RadioGroup.Options`, `Select(options)` and `OptionGroup.Options` leave
      (regenerate after the removal), and `Choice.Label  text  kept: …` is written BY HAND in the family that
      owns `Choice` (inputs and pickers, beside `OptionGroup`), with its reason; the list shrinks by four
- [ ] 7.3 `Fixtures/roslyn-completions.fixture.json` regenerated if its test reads these names, its diff read
- [ ] 7.4 `docs/HANDOFF-FIDELITY-AUDIT.md`: the fenced evidence of C14 (Select), the Tabs entry and the
      SegmentedControl entry quote the new constructors (`DocsIndexTests` finds a citation by its quote)
- [ ] 7.5 Check: the Web, Native.Engine, Conformance, Compiler and Testing test projects pass, one at a time

## 8. Documentation

- [ ] 8.1 Wiki, on the branch `feat/a-choice-answers-the-apps-own-value` of the wiki: `Components` and
      `DeclarativeSurface` in all five languages, `Upgrading` in English and pt-BR with the migration line of
      design.md and the `*REMOVED*` lines
- [ ] 8.2 One ledger entry under `docs/ledger/`, citing #1516
- [ ] 8.3 README captures: retake `web-dashboard.png` (`./scripts/capture-readme.sh web compose`), look at it,
      record with `EQ_UPDATE_README_CAPTURES=1`. `desktop-gallery.png`, `desktop-gallery-light.png`,
      `ios-home.png` and `ios-transfer.png` need a Mac, and `android-transactions.png` and `android-cards.png` a
      booted emulator: open question 1 of design.md, for the lead
- [ ] 8.4 Check: the docs guards (`DocsIndexTests`, `ReadmeCapturesTests`, the wiki guards with `EQ_WIKI_DIR` on
      the wiki branch) pass; `./scripts/check-openspec.sh` passes
