Each section is one sub-issue of #1966, in the order they are built: the truth first, the engine on it, the
vocabulary before the realizers, a realizer's C# with its TypeScript twin, the component on all of them, and
the documentation last. A section ends with the check that proves it against the real thing.

## 1. The corpus: .NET's MaskedTextProvider and MaskedTextBox's editing, measured and pinned (3 points)

- [ ] 1.1 In `tests/eQuantic.UI.Conformance.Tests/Masks/`, a corpus generator over the real
  `System.ComponentModel.MaskedTextProvider`, from the draft `measure/measure.cs`: every case of its two
  layers (the provider's operations with the state after each, and the positions), and the masks, cultures
  and options it measured (CPF plain and escaped, both phones, CEP, the date in pt-BR, en-US and de-DE, the
  money mask in en-US, pt-BR and de-DE, `>LL-0000`, `<LL|LL>LL`, the escapes, `00:00` in fi-FI, the options one
  by one, fullwidth and Arabic-Indic digits). Test: `MaskCorpusTests.TheCorpus_IsWhatDotNetAnswers`, which
  regenerates in memory and fails naming the first case that differs from `masked-text-provider.corpus.json`.
- [ ] 1.2 `MaskedTextBox`'s composites (`PlaceChar`, `OnKeyPress`, `Delete`, `Replace`, the `Text` setter),
  transcribed into the test project over the real provider, each method citing winforms
  `MaskedTextBox.cs` at `3248f2c4b8839706952bb7d863a0b2b957b745f7`, and the editing layer of the corpus written
  through them. Test: the same corpus test, editing cases included (the caret after each step).
- [ ] 1.3 `EQ_UPDATE_MASK_CORPUS=1` writes the file, and only then. Test:
  `MaskCorpusTests.TheCorpus_IsNotRewrittenWithoutTheVariable`, proved both ways: a hand-edited case fails
  without the variable, and the file is unchanged after the failing run.
- [ ] 1.4 Check against the real thing: run the corpus test on the macOS host, the Linux CI job and the Windows
  runner with no `EQ_UPDATE_MASK_CORPUS`; a culture whose data differs between them fails here, and is then
  dropped from the corpus or pinned per platform, as the design's risks say.

## 2. A page reads its culture's date and time separators (2 points)

- [ ] 2.1 eqc carries `DateTimeFormatInfo.DateSeparator` and `TimeSeparator` as it carries `NumberFormatInfo`'s
  members (`PlatformMemberRead`), to the runtime's culture data (`utils/culture.ts`). Test: a conformance case
  `CultureSeparatorsConformanceTests` that reads both in en-US, de-DE, fi-FI and pt-BR on both sides, failing
  before the change with EQ2006 and passing after.
- [ ] 2.2 Check against the real thing: the conformance suite executed on both sides.

## 3. The mask engine, in C#, answers what MaskedTextProvider answers (8 points)

- [ ] 3.1 `internal sealed class MaskEngine` in `eQuantic.UI.Components` (a folder `Masking/`, one type per
  file): the mask parsed into a descriptor per position, the culture's literals resolved at construction, the
  case conversions and the escapes, and the errors .NET throws for an empty mask. Test: `MaskEngineTests`
  over the corpus's `positions` (edit positions and the three finds, both directions).
- [ ] 3.2 The provider's operations by .NET's names and the hints `MaskedTextResultHint` reports, as an internal
  enum. Test: `MaskEngineTests.EveryProviderCase_AnswersAsDotNet`, every provider case of the corpus.
- [ ] 3.3 `MaskedTextBox`'s composites over the engine: `Type`, `Delete(backward)`, `Paste`, `SetText`. Test:
  `MaskEngineTests.EveryEditingCase_AnswersAsMaskedTextBox`.
- [ ] 3.4 `MaskFormatter : ITextInputFormatter` (needs 5.1's interface; until then, the methods without the
  interface), `Read` and `Fit`, and `MaskedText`, with the text drawn as design decision 4 says and the state
  rebuilt from it position by position. Test: `MaskFormatterTests`, the round trips of the spec's scenarios
  (an optional gap, raw and formatted values) and a seeded session of 2,000 edits per corpus mask compared
  against the transcribed composites over the real provider.
- [ ] 3.5 Prove the tests discriminate (PROVING §2): each fails with the most plausible wrong engine (a
  Backspace that deletes the literal, a paste that stops at the first refused character, a `.` read as a
  literal), kept as cases of the seeded session.
- [ ] 3.6 Declare the surface: `MaskFormatter`, `MaskedText` in `src/eQuantic.UI.Components/PublicAPI.Unshipped.txt`.
  Check against the real thing: the conformance project's .NET side over the whole corpus.

## 4. The mask engine in the browser answers what .NET answers (3 points)

- [ ] 4.1 The engine and `MaskFormatter` transpile into the runtime with the rest of Components (their modules in
  `src/eQuantic.UI.Runtime/src/shared/components/`), with no EQ diagnostic. Test: the compiler's component pins
  (`EQ_UPDATE_TRANSPILED=1` once, then compared).
- [ ] 4.2 `MaskTwinTests` in `tests/eQuantic.UI.Conformance.Tests`, as `CodeLinesTwinTests` runs the code engine:
  every corpus case and the seeded sessions run on the twin in the served `runtime.js` under the embedded Bun,
  compared to .NET's answers, the Unicode classes' edges included (fullwidth, Arabic-Indic, combining marks,
  letters outside the BMP).
- [ ] 4.3 Check against the real thing: the twin suite executed on both sides, and one corpus case run in Chrome
  through the served bundle (`tests/e2e`), since Bun's ICU is not a browser's.

## 5. A formatter decides every edit of a text entry, on the web (8 points)

- [ ] 5.1 The vocabulary: `ITextInputFormatter`, `TextEdit`, `TextEditKind` (append-only, `EnumValueAbiTests`) and
  `TextEntry.Formatter` in Primitives; `TextEditingValue` moved from `eQuantic.UI.Native.Components` to
  Primitives, every use in the tree moved with it, the old lines `*REMOVED*` in
  `src/eQuantic.UI.Native.Components/PublicAPI.Unshipped.txt` and the new ones in Primitives'. The runtime's
  TypeScript twins of the three (`shared/nodes.ts`, `vocabulary.ts`, the generated enum union). Test:
  `EnumValueAbiTests`, `ExportParity` (`export-parity.spec.ts`), and the build with RS0016/RS0017.
- [ ] 5.2 `TextInput` and `FormInput` take `formatter` and hand it to their entry, carried by `AdoptConfig`.
  Test: `TextInputTests.AFormatter_ReachesTheEntry_AcrossARebuild` (a parent handing a second config).
- [ ] 5.3 The web realizer's C# (`WebLoweringVisitor.LowerTextEntry`) and its twin (`lowering.ts`,
  `lowerTextEntry`): a formatted entry is not prose (`spellcheck`, `autocapitalize`, `autocorrect`), and the
  twin installs the `beforeinput` handler of design decision 5 (the table of input types), the composition rule
  (`compositionstart` keeps the state, `compositionend` applies the committed text once, the `input` after it
  ignored), and calls `onChanged` itself. Test: `text-entry-formatter.spec.ts` (vitest), driven as the browser
  drives it: `beforeinput` of each input type with its `cancelable` and `isComposing`, a composition sequence as
  Chrome sends it and as Firefox sends it, `historyUndo`; and the component parity fixture regenerated.
- [ ] 5.4 Check against the real thing: a Playwright walk in Chrome and Firefox (`tests/e2e`) over a sample
  field with a test formatter: typing in the middle keeps the caret where the formatter put it (fails on the
  `input`-rewrite road, measured), Backspace and Delete, a paste, `Meta+Z`, and Chrome's CDP composition.

## 6. A formatter decides every edit of a text entry, on Photon (8 points)

- [ ] 6.1 `PhotonHost` asks the entry's formatter at every funnel before `Commit`: `TextInput`, the paste, cut,
  Backspace and Delete (with the word and line variants), `CommitText`; `SetMarkedText` composes unformatted and
  the commit is one typed edit. Test: `PhotonFormatterTests` in the native tests, crossing a frame after each
  key (PROVING §10), with a test formatter, and the same cases as 5.3.
- [ ] 6.2 `UpdateEditingValue` derives one `TextEdit` from the whole new value (design decision 5), keeps a
  composing value as sent, and the session hands the keyboard the formatter's answer. Test:
  `PhotonFormatterTests.APhonesWholeValue_ReachesTheFormatterAsOneEdit`: a digit, a Backspace, a selection
  replaced, an autocorrected word, a composition then its commit.
- [ ] 6.3 The shells: `TextInputSession` carries whether the field is prose; `PhotonKeyboard` (iOS) and
  `PhotonInputConnection` (Android) open a non-prose field with no autocorrection, suggestions or capitals.
  Test: the session's unit tests for the flag; the shells' as below.
- [ ] 6.4 Check against the real thing: the PhotonDesktop sample on macOS (typing, a dead key, a paste), the
  Windows runner (`gh workflow run ci.yml --ref <branch>` with the Windows job), the iOS 27 simulator and the
  Android emulator (a postal code's keypad, a plate's keyboard with no suggestion bar), each with a screenshot
  and the field's text read back from the host.

## 7. MaskedInput takes the shape of its mask, served and hydrated (5 points)

- [ ] 7.1 `MaskedInput` in Components, composed from `TextInput` with a `MaskFormatter`: the signature of design
  decision 4, the kind derived from the mask, the placeholder, `MaskedText` handed on, the text kept while the
  page's value fits to it (`AdoptConfig`), the incomplete error when left. The factory is generated
  (`UI.MaskedInput`). Test: `MaskedInputTests` (the spec's scenarios), `CatalogCustomizationTests` (it takes a
  `style`), and `PublicAPI.Unshipped.txt`.
- [ ] 7.2 SSR draws the field with the C# engine and hydration draws the same text with the twin. A field typed
  into before hydration is fitted once when it hydrates (open question 6; if the owner chooses to leave it, the
  task changes to proving it is left). Test: a hydration case in `tests/HydrationSite` with a CPF field served
  empty, served holding a value, and typed into before the runtime loads (`renderer-adopt.spec.ts` for the
  runtime half).
- [ ] 7.3 Check against the real thing: the dashboard sample served and hydrated in Chrome and Firefox, with the
  caret and the text read after hydration, and a navigation back to the page (hydration defects show there).

## 8. The server validates a masked value with MaskedTextProvider (3 points)

- [ ] 8.1 `[ServerOnly] MaskAttribute : ValidationAttribute` in Primitives, with `AsciiOnly`, over .NET's
  `MaskedTextProvider` in the current culture. Test: `MaskAttributeTests` through
  `Validator.TryValidateObject` with the spec's four values in pt-BR and in the invariant culture.
- [ ] 8.2 `FormModelGenerator` reads `MaskAttribute` (by its namespace, as it reads DataAnnotations') and emits
  the engine's rule. Test: the generator's snapshot test, and a conformance case running the generated rule in
  the browser with the same four values.
- [ ] 8.3 Check against the real thing: a server action in the dashboard sample that validates a posted model,
  called from the browser with a complete and an incomplete CPF.

## 9. Assistive technology hears what a masked field holds and what it still asks for (5 points)

- [ ] 9.1 `MaskedInput` adds `SdkStrings.MaskRemaining(count)` to its entry's description while incomplete, and
  `SdkStrings.MaskIncomplete` as its error once left; the strings in every resx the SDK ships (en, pt, pt-BR,
  es) and their TypeScript twin regenerated (`SdkStringsTsGeneratorTests`). Test: `MaskedInputTests` for the
  description and the error.
- [ ] 9.2 `SemanticNode.Description` in Primitives, filled from `TextEntry.Description` by
  `SemanticsVisitor.Text`, and handed by each shell to its platform (`accessibilityHelp`, `accessibilityHint`,
  `setHintText`, UI Automation's `HelpText`). Test: `SemanticsTests` for the node; the shells' accessibility
  bridges' tests for the mapping.
- [ ] 9.3 Check against the real thing: VoiceOver's reading of the web field in Safari on macOS and of the
  PhotonDesktop field (the accessibility inspector's value and help), the iOS simulator's Accessibility
  Inspector, and Android's `uiautomator dump` for the hint text.

## 10. The wiki and the dashboard show MaskedInput (3 points)

- [ ] 10.1 A wiki page `MaskedInput` in the five languages (English at the root, `locale/<culture>/` for pt-BR,
  es, fr and de), on a wiki branch named as this change's branch: the mask language with the culture's
  separators (the CPF written `000\.000\.000-00`, and why), the values handed on, the keyboard, server
  validation with `[Mask]`, what is fenced. The `Upgrading` page carries the `TextEditingValue` move. Test: the
  wiki guards run against the branch (`EQ_WIKI_DIR`).
- [ ] 10.2 The dashboard sample shows a CPF, a phone and a postal code field in its forms screen; if the README's
  capture of that screen changes, it is taken again (`./scripts/capture-readme.sh`), looked at, and recorded
  (`ReadmeCapturesTests`).
- [ ] 10.3 A ledger entry under `docs/ledger/` citing #1966. Test: `LedgerTests`.
- [ ] 10.4 Archive the change with `openspec archive field-takes-the-shape-of-its-mask --yes` before the last
  merge. Check: `./scripts/check-openspec.sh`.
