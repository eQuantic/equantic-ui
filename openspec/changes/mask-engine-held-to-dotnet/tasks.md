The first of the five changes that ship the masked field (#1966), pull request A. The truth comes first, then the assembly and the engine on it, and the vocabulary the engine implements last. A section ends with the check that proves it against the real thing. Its heading names the sub-issue it closes. The shared design is `mask-engine-held-to-dotnet`'s, cited by decision number.

## 1. The corpus: .NET's MaskedTextProvider and MaskedTextBox's editing, measured and pinned (3 points, #1968)

- [ ] 1.1 A corpus generator over the real `System.ComponentModel.MaskedTextProvider`, in
  `tests/eQuantic.UI.Conformance.Tests/Masks/`, from the draft `measure/measure.cs`.
  - It covers both layers: the provider's operations with the state after each, the password-ignoring and display
    forms included, and the positions.
  - It covers the masks, cultures and options measured: the CPF plain and escaped, both phones, the CEP, the date
    in pt-BR, en-US and de-DE, the money mask in en-US, pt-BR and de-DE, `>LL-0000`, `<LL|LL>LL`, the escapes,
    `00:00` in fi-FI, each option alone, a `PasswordChar`, and fullwidth and Arabic-Indic digits.
  - Test: `MaskCorpusTests.TheCorpus_IsWhatDotNetAnswers`. It regenerates the corpus in memory and fails naming the
    first case that differs from `masked-text-provider.corpus.json`.
- [ ] 1.2 `MaskedTextBox`'s composites, transcribed into the test project over the real provider: `PlaceChar` with
  `overwrite`, `OnKeyPress`, `Delete`, `Replace` and the `Text` setter.
  - Each method cites winforms `MaskedTextBox.cs` at `3248f2c4b8839706952bb7d863a0b2b957b745f7`.
  - The corpus's editing layer is written through them, the three overwrite cases measured included.
  - Test: the same corpus test, editing cases included, with the caret after each step.
- [ ] 1.3 `EQ_UPDATE_MASK_CORPUS=1` writes the file, and only then.
  - Test: `MaskCorpusTests.TheCorpus_IsNotRewrittenWithoutTheVariable`, proved both ways: a hand-edited case fails
    without the variable, and the file is unchanged after the failing run.
- [ ] 1.4 Check against the real thing: run the corpus test on the macOS host, the Linux CI job and the Windows
  runner, with no `EQ_UPDATE_MASK_CORPUS`. A culture whose data differs between them fails here. It is then
  dropped from the corpus or pinned per platform, as the design's risks say.

## 2. A page reads its culture's date and time separators (2 points, #1969)

- [ ] 2.1 eqc carries `DateTimeFormatInfo.DateSeparator` and `TimeSeparator` to the runtime's culture data
  (`utils/culture.ts`), as it carries `NumberFormatInfo`'s members (`PlatformMemberRead`).
  - Test: a conformance case, `CultureSeparatorsConformanceTests`, reads both in en-US, de-DE, fi-FI and pt-BR on
    both sides. It fails before the change and passes after.
- [ ] 2.2 Check against the real thing: the conformance suite, executed on both sides.

## 3. eQuantic.UI.Masking is an assembly the SDK delivers (3 points, #1970)

- [ ] 3.1 `src/eQuantic.UI.Masking/eQuantic.UI.Masking.csproj`, as `eQuantic.UI.Sheet.csproj` is:
  - `net10.0`, `IsAotCompatible`, no `InvariantGlobalization`, a `ProjectReference` to Primitives;
  - `PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`;
  - `InternalsVisibleTo` for the conformance tests;
  - an entry in `eQuantic.UI.sln`.
  - Test: the build, with RS0016/RS0017, and `scripts/check-package-surface.cs` reading it as `contract`.
- [ ] 3.2 Both SDKs' `Sdk.props` reference it:
  - the `PackageReference` at `$(EQuanticUIVersion)`, and the `ProjectReference` under `IsEQuanticDevMode`;
  - `<Using Include="eQuantic.UI.Masking" />` beside the static usings.
  - `eQuantic.UI.Components` references it.
  - Test: `DeveloperSurfaceContractTests` is unchanged (no property moves), and a template app built from a local
    feed compiles a page that names `MaskedText` with no `using` (the BuildFlow recipe).
- [ ] 3.3 The runtime provides its types:
  - `RuntimeProvidedTypeScanner.VocabularyNamespaces` and `.RuntimeAssemblies` name it;
  - `SharedComponentTranspilationTests.EngineSources` takes its directory;
  - `EnumUnionsTsGenerator` reads its enums;
  - the conformance csproj and `Infrastructure/ModuleGraph.cs` reference it.
  - Test: `VocabularyInterfaceDefaultsTests`, and the component pins (`EQ_UPDATE_TRANSPILED=1` once, then
    compared).
- [ ] 3.4 Check against the real thing: `dotnet pack` to a local feed, and an app restored from it with only its
  SDK named, served in Chrome, importing the engine from `@equantic/runtime`.

## 4. The mask engine answers what MaskedTextProvider answers, in C# (5 points, #1971)

- [ ] 4.1 `internal sealed class MaskEngine`, one type per file under `src/eQuantic.UI.Masking/`:
  - the mask parsed into a descriptor per position;
  - the culture's literals resolved at construction;
  - the case conversions and the escapes;
  - the errors .NET throws for an empty mask.
  - Test: `MaskEngineTests` over the corpus's `positions`: the edit positions and the three finds, both directions.
- [ ] 4.2 The provider's operations by .NET's names, the `PasswordChar` forms of `ToString` and
  `ToDisplayString`, and the hints as an internal enum.
  - Test: `MaskEngineTests.EveryProviderCase_AnswersAsDotNet`, over every provider case of the corpus.
- [ ] 4.3 Prove the test discriminates (PROVING §2): it fails with a `.` read as a literal, with a `RemoveAt` that
  removes a literal, and with a `Set` that keeps what it did not reach.
- [ ] 4.4 Check against the real thing: the conformance project's .NET side, over the whole provider layer.

## 5. The mask edits as MaskedTextBox edits, and keeps a history (5 points, #1972)

- [ ] 5.1 `MaskedTextBox`'s composites over the engine: `Type` (insert and overwrite), `Delete(backward)`, `Paste`
  and `SetText`.
  - Test: `MaskEngineTests.EveryEditingCase_AnswersAsMaskedTextBox`.
- [ ] 5.2 `MaskFormatter : ITextInputFormatter` (needs 7.1), `InsertKeyMode`, `Read`, `Fit`, `IsLiteral`,
  `Overwriting`, and `MaskedText`.
  - The text is drawn as design decision 4 says, and the state is rebuilt from it position by position.
  - Test: `MaskFormatterTests`, with the round trips of the spec's scenarios (an optional gap, raw and formatted
    values).
  - Test: a seeded session of 2,000 edits per corpus mask, compared against the transcribed composites over the
    real provider.
- [ ] 5.3 The history (design decision 8): runs of typing coalesce, a new step drops the redo branch, the limit is
  100 steps, and a `current` the formatter did not answer clears it.
  - Test: `MaskFormatterHistoryTests`, the spec's "Undo a paste" and "A reset is not undone", and the redo branch
    dropped. Each is proved failing with a history that is not cut on reset.
- [ ] 5.4 Declare the surface in `src/eQuantic.UI.Masking/PublicAPI.Unshipped.txt`.
  - Check against the real thing: the seeded sessions on .NET, against the real provider.

## 6. The mask engine in the browser answers what .NET answers (3 points, #1973)

- [ ] 6.1 The engine and `MaskFormatter` transpile with no EQ diagnostic.
  - Test: the component pins.
- [ ] 6.2 `MaskTwinTests` in `tests/eQuantic.UI.Conformance.Tests`, as `CodeLinesTwinTests` runs the code engine.
  - Every corpus case, the seeded sessions and the history run on the twin, in the served `runtime.js` under the
    embedded Bun, compared to .NET's answers.
  - The Unicode classes' edges are included: fullwidth, Arabic-Indic, combining marks, and letters outside the
    BMP.
- [ ] 6.3 Check against the real thing: the twin suite executed on both sides, and one corpus case run in Chrome
  through the served bundle (`tests/e2e`), since Bun's ICU is not a browser's.

## 7. The vocabulary: a formatter and its commands on a text entry (3 points, #1974)

- [ ] 7.1 In Primitives: `ITextInputFormatter` (`Format`, `Command`, `Prose`, `Overwriting`, `IsLiteral`),
  `TextEdit`, `TextEditKind`, `TextCommand` and `TextEntry.Formatter`.
  - `TextEditingValue` moves from `eQuantic.UI.Native.Components` to Primitives, and every use in the tree moves
    with it.
  - The old lines are `*REMOVED*` in `src/eQuantic.UI.Native.Components/PublicAPI.Unshipped.txt`, and the new
    ones are added to Primitives'.
  - The runtime's TypeScript twins come with them (`shared/nodes.ts`, `vocabulary.ts`, the generated enum
    unions).
  - Test: `EnumValueAbiTests` (the two enums are append-only), `export-parity.spec.ts`, and the build.
- [ ] 7.2 `TextInput` and `FormInput` take `formatter` and hand it to their entry, carried by `AdoptConfig`.
  - Test: `TextInputTests.AFormatter_ReachesTheEntry_AcrossARebuild`, with a parent handing a second config.
- [ ] 7.3 Check against the real thing: the component parity fixture is regenerated, and a field with a test
  formatter renders on both realizers.
- [ ] 7.4 The documentation of what this change ships, in its own pull request: the wiki's `Upgrading` page in the five
  languages carries the `TextEditingValue` move (`eQuantic.UI.Native.Components` → `eQuantic.UI.Primitives`), and a
  ledger entry under `docs/ledger/` cites #1966.
  - Test: the wiki guards against the change's wiki branch (`EQ_WIKI_DIR`), and `LedgerTests`.
- [ ] 7.5 Archive the change with `openspec archive mask-engine-held-to-dotnet --yes` before its pull request merges.
  - Check: `./scripts/check-openspec.sh`.
