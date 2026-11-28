Each section is one sub-issue of #1966, of at most 8 points, in the order they are built. The truth comes first,
then the assembly and the engine on it. The vocabulary comes before the realizers, and a realizer's C# comes with
its TypeScript twin. The component comes on all of them, and the documentation travels with the behaviour it
describes. A section ends with the check that proves it against the real thing. The pull request each section
travels in is named after its title (A to E, the report's Revision 1 gives their order).

## 1. The corpus: .NET's MaskedTextProvider and MaskedTextBox's editing, measured and pinned (3 points, PR A)

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

## 2. A page reads its culture's date and time separators (2 points, PR A)

- [ ] 2.1 eqc carries `DateTimeFormatInfo.DateSeparator` and `TimeSeparator` to the runtime's culture data
  (`utils/culture.ts`), as it carries `NumberFormatInfo`'s members (`PlatformMemberRead`).
  - Test: a conformance case, `CultureSeparatorsConformanceTests`, reads both in en-US, de-DE, fi-FI and pt-BR on
    both sides. It fails before the change and passes after.
- [ ] 2.2 Check against the real thing: the conformance suite, executed on both sides.

## 3. eQuantic.UI.Masking is an assembly the SDK delivers (3 points, PR A)

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

## 4. The mask engine answers what MaskedTextProvider answers, in C# (5 points, PR A)

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

## 5. The mask edits as MaskedTextBox edits, and keeps a history (5 points, PR A)

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

## 6. The mask engine in the browser answers what .NET answers (3 points, PR A)

- [ ] 6.1 The engine and `MaskFormatter` transpile with no EQ diagnostic.
  - Test: the component pins.
- [ ] 6.2 `MaskTwinTests` in `tests/eQuantic.UI.Conformance.Tests`, as `CodeLinesTwinTests` runs the code engine.
  - Every corpus case, the seeded sessions and the history run on the twin, in the served `runtime.js` under the
    embedded Bun, compared to .NET's answers.
  - The Unicode classes' edges are included: fullwidth, Arabic-Indic, combining marks, and letters outside the
    BMP.
- [ ] 6.3 Check against the real thing: the twin suite executed on both sides, and one corpus case run in Chrome
  through the served bundle (`tests/e2e`), since Bun's ICU is not a browser's.

## 7. The vocabulary: a formatter and its commands on a text entry (3 points, PR A)

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

## 8. A formatter decides every edit of a text entry on the web (8 points, PR B)

- [ ] 8.1 The web realizer's C# (`WebLoweringVisitor.LowerTextEntry`) and its twin (`lowering.ts`,
  `lowerTextEntry`).
  - A formatted entry is not prose: `spellcheck`, `autocapitalize`, `autocorrect`.
  - The twin's `beforeinput` handler follows the table of design decision 5.
  - It writes through `execCommand('insertText')` under a guard, and falls back to assigning `value` where
    `execCommand` is absent.
  - It calls `onChanged` itself.
  - Test: `text-entry-formatter.spec.ts` (vitest), driven as the browser drives the field: a `beforeinput` of each
    input type, with its `cancelable` and `isComposing`.
- [ ] 8.2 The composition rule: `compositionstart` keeps the state, `compositionend` applies the committed text
  once, and the `input` after it is ignored.
  - Test: the same spec, with a composition sequence as Chrome sends it and as Firefox sends it.
- [ ] 8.3 The commands: `keydown` `Ctrl`/`Cmd+Z`, `Ctrl`/`Cmd+Shift+Z`, `Ctrl+Y` and `Insert`, and `historyUndo` and
  `historyRedo`, are cancelled and handed to the formatter. `caret-shape: block` is set while `Overwriting`.
  - Test: the same spec, including a formatter that takes no command, for which the browser's own action stands.
- [ ] 8.4 Check against the real thing: a Playwright walk in Chrome and Firefox (`tests/e2e`) over a sample field
  with a test formatter.
  - Typing in the middle keeps the caret where the formatter put it. This fails on the `input`-rewrite road, as
    measured.
  - Backspace and Delete, a paste, and `Meta+Z` then `Meta+Shift+Z`.
  - Chrome's CDP composition.
  - `Insert` and the block caret in Chrome.

## 9. An obscured formatted entry shows its literals over a real password input (5 points, PR B)

- [ ] 9.1 The C# lowering and its twin: an obscured formatted entry is a `type="password"` input holding the whole
  text, with an `aria-hidden`, `pointer-events: none` overlay of the literals.
  - The overlay is placed at each literal's index times the bullet width measured from the input, and follows its
    `scroll` (observed, never sampled). It has the field's fill behind each glyph.
  - Test: a vitest spec over a jsdom-free layout stub for the placement, and the SSR markup test in
    `SurfaceSsrTests`.
- [ ] 9.2 Copy is refused, `autocomplete` is kept, and the overlay survives a re-render without replacing the input
  (focus is kept).
  - Test: the parity fixture, and a vitest case that re-renders while focused.
- [ ] 9.3 Check against the real thing: Playwright in Chrome and Firefox.
  - A literal stays over its cell while the field scrolls.
  - `Meta+C` leaves the clipboard as it was in Chrome.
  - The input is of type `password` and the overlay is absent from the accessibility tree.

## 10. A formatter decides every edit of a text entry on Photon (5 points, PR C)

- [ ] 10.1 `PhotonHost` asks the entry's formatter at every funnel before `Commit` (design decision 5's table).
  `SetMarkedText` composes unformatted, and the commit is one typed edit.
  - Test: `PhotonFormatterTests` in the native tests, crossing a frame after each key (PROVING §10), with a test
    formatter and the same cases as 8.1 and 8.2.
- [ ] 10.2 `UpdateEditingValue` derives one `TextEdit` from the whole new value, keeps a composing value as sent,
  and the session hands the keyboard the formatter's answer.
  - Test: `PhotonFormatterTests.APhonesWholeValue_ReachesTheFormatterAsOneEdit`, with a digit, a Backspace, a
    selection replaced, an autocorrected word, and a composition then its commit.
- [ ] 10.3 The commands in `EditKey`, the block caret, and an obscured entry drawn `•` only where `IsLiteral` says
  no (`EmitVisitor.Text`).
  - Test: `PhotonFormatterTests` for the commands, and a golden of the obscured field with an assertion of its
    glyphs beside it (PROVING §8).
- [ ] 10.4 Check against the real thing: the PhotonDesktop sample on macOS, with typing, a dead key, a paste,
  `Cmd+Z` and an obscured field.

## 11. The shells take the formatter's keyboard and undo (8 points, PR C)

- [ ] 11.1 `TextInputSession` carries whether the field is prose. `PhotonKeyboard` (iOS) and
  `PhotonInputConnection` (Android) open a non-prose field with no autocorrection, suggestions or capitals.
  - An obscured mask of digits is `NumberVariationPassword` on Android.
  - Test: the session's unit tests.
- [ ] 11.2 The macOS shell's main menu gains Edit › Undo and Redo, sent to the host as the commands while a text
  target holds the caret.
  - Test: the shell's menu test, and the probe that reads the menu's state.
- [ ] 11.3 iOS: `PhotonKeyboard`'s proxy answers `undoManager` with an `NSUndoManager` whose actions call the host,
  so the shake and the three-finger gestures reach the formatter.
  - Android: `KEYCODE_INSERT` and `Ctrl+Z` from a hardware keyboard reach `EditKey`.
  - Windows: `Ctrl+Z`, `Ctrl+Y` and `Insert`.
  - Test: each shell's input tests.
- [ ] 11.4 Check against the real thing, each with a screenshot and the field's text read back from the host:
  - the Windows runner (`gh workflow run ci.yml --ref <branch>`, with the Windows job), for `Insert` and
    `Ctrl+Z`;
  - the iOS 27 simulator (XCUITest), for a postal code's `NumberPad`, a plate's keyboard with no suggestion bar,
    the secure keyboard of an obscured field, and Device › Shake undoing;
  - the Android emulator (`adb shell input`), for `ClassNumber`, no suggestions and `NumberVariationPassword`.

## 12. MaskedInput takes the shape of its mask, served and hydrated (5 points, PR D)

- [ ] 12.1 `MaskedInput` in Components, composed from `TextInput` with one `MaskFormatter` per instance kept across
  rebuilds.
  - It has the signature of design decision 4, the kind derived from the mask, the placeholder, and `MaskedText`
    handed on.
  - It keeps the text while the page's value fits to it (`AdoptConfig`), and shows the incomplete error when left.
  - It takes `InsertKeyMode` and `Obscure`.
  - The factory is generated (`UI.MaskedInput`).
  - Test: `MaskedInputTests` (the spec's scenarios), `CatalogCustomizationTests` (it takes a `style`), and
    `PublicAPI.Unshipped.txt`.
- [ ] 12.2 SSR draws the field with the C# engine, and hydration draws the same text with the twin. A field typed
  into before hydration is fitted once when it hydrates.
  - Test: a hydration case in `tests/HydrationSite`, with a CPF field served empty, served holding a value, served
    obscured, and typed into before the runtime loads. `renderer-adopt.spec.ts` covers the runtime half.
- [ ] 12.3 Check against the real thing: the dashboard sample served and hydrated in Chrome and Firefox. The caret
  and the text are read after hydration, and after a navigation back to the page, where hydration defects show.

## 13. A masked field parses into a typed value, as the server does (5 points, PR D)

- [ ] 13.1 First, measure that eqc carries `new MaskedValueParser<DateOnly>(DateOnly.TryParse)`: a BCL method group
  with an `out` parameter into a custom generic delegate.
  - If it does not, isolate the smallest case and fix it in the compiler first (principle 14), inside this
    section's points if it is small, or as a sub-issue of its own.
  - Test: a conformance case over `DateOnly`, `TimeOnly`, `DateTime`, `int` and `decimal`.
- [ ] 13.2 `IMaskedValueParser` and `MaskedValueParser<TValue>` in Masking. `MaskedInput`'s `validatingType` shows
  `SdkStrings.MaskValueInvalid` when the field is left completed but unparsable.
  - Test: `MaskedInputTests`, and the spec's "A date in Brazil" on both sides.
- [ ] 13.3 `MaskAttribute.ValidatingType` calls the type's static `TryParse(string, IFormatProvider, out T)`.
  - Test: `MaskAttributeTests`, and a conformance case holding the type's `TypeConverter`, the server's
    `TryParse` and the twin's `TryParse` together on the measured dates and numbers.
- [ ] 13.4 Check against the real thing: the dashboard's date field, typed and left in Chrome with a valid and an
  invalid date.

## 14. The server validates a masked value with MaskedTextProvider (3 points, PR D)

- [ ] 14.1 `[ServerOnly] MaskAttribute : ValidationAttribute` in Masking, with `AsciiOnly`, over .NET's
  `MaskedTextProvider` in the current culture.
  - Test: `MaskAttributeTests` through `Validator.TryValidateObject`, with the spec's four values in pt-BR and in
    the invariant culture.
- [ ] 14.2 `FormModelGenerator` reads `MaskAttribute` (by its namespace) and emits the engine's rule, with
  `ValidatingType` when it is given.
  - Test: the generator's snapshot test, and a conformance case running the generated rule in the browser with the
    same four values.
- [ ] 14.3 Check against the real thing: a server action in the dashboard that takes the model, called from the
  browser with a complete and an incomplete CPF (this rides on section 15 when it has merged, and on a hand call to
  `Validator` before).

## 15. A Server Action validates its arguments before it runs (5 points, PR E)

- [ ] 15.1 `ServerActionArgumentValidator` in the Server: each parameter's attributes through
  `Validator.TryValidateValue`, each model through `Validator.TryValidateObject`, and the recursive walk over model
  properties and collection elements.
  - Each type's members are read once and cached. Errors are keyed by path (`Address.PostalCode`,
    `Items[0].Name`), and `RequiresUnreferencedCode` is declared.
  - Test: `ServerActionValidationTests`, with the spec's nested model, a scalar parameter, a collection, an
    `IValidatableObject`, and a cycle in the graph that does not loop.
- [ ] 15.2 `ServerActionsMiddleware` validates before invoking, and answers `400` `application/problem+json` in
  `HttpValidationProblemDetails`'s shape, with messages in the request's culture.
  - Test: `ServerActionsMiddlewareTests`, which shows the action is not invoked, the body, and the pt-BR messages.
- [ ] 15.3 Check against the real thing: the endpoint called with an HTTP client in the server tests' test host
  (`WebApplicationFactory`), with a valid and an invalid body.

## 16. A page hears the server's verdict, and a form shows it (3 points, PR E)

- [ ] 16.1 `ServerValidationException` (`[TwinIsTranspiled]`, `eQuantic.UI.Primitives.Forms`), with `Errors`.
  `server-actions.ts` throws it on a `400` validation problem, and a plain error otherwise, as today.
  - Test: `server-actions.spec.ts`, and a conformance case catching it in C#.
- [ ] 16.2 `FormController.SubmitAsync` catches it and calls `ApplyServerErrors`. A path that names no field goes to
  `SubmitError`.
  - Test: `FormControllerTests`, and the transpiled twin's spec.
- [ ] 16.3 The wiki's server actions page and the forms page, in the five languages, say that arguments are
  validated and how a form shows it. A ledger entry cites #1966. `Upgrading` carries the behaviour change.
  - Test: the wiki guards (`EQ_WIKI_DIR`), and `LedgerTests`.
- [ ] 16.4 Check against the real thing: the dashboard's form submitted in Chrome with an invalid postal code,
  showing the server's message on the field.

## 17. Assistive technology hears what a masked field holds and what it still asks for (5 points, PR D)

- [ ] 17.1 `MaskedInput` adds `SdkStrings.MaskRemaining(count)` to its entry's description while incomplete, and
  `SdkStrings.MaskIncomplete` and `MaskValueInvalid` as its errors once left.
  - The strings go in every resx the SDK ships (en, pt, pt-BR, es), and their TypeScript twin is regenerated
    (`SdkStringsTsGeneratorTests`).
  - Test: `MaskedInputTests`, for the description and the errors.
- [ ] 17.2 `SemanticNode.Description` in Primitives, filled from `TextEntry.Description` by
  `SemanticsVisitor.Text`. Each shell hands it to its platform: `accessibilityHelp`, `accessibilityHint`,
  `setHintText`, and UI Automation's `HelpText`.
  - Test: `SemanticsTests` for the node, and the shells' accessibility bridges' tests for the mapping.
- [ ] 17.3 Check against the real thing:
  - VoiceOver's reading of the web field in Safari on macOS, and of the PhotonDesktop field (the accessibility
    inspector's value and help);
  - the iOS simulator's Accessibility Inspector;
  - Android's `uiautomator dump`, for the hint text.

## 18. The wiki and the dashboard show MaskedInput (3 points, PR D)

- [ ] 18.1 A wiki page, `MaskedInput`, in the five languages (English at the root, `locale/<culture>/` for pt-BR,
  es, fr and de), on a wiki branch named as the pull request's branch. It covers:
  - the mask language with the culture's separators (the CPF written `000\.000\.000-00`, and why);
  - the values handed on, overwrite, obscure, `ValidatingType`, undo, the keyboard, and `[Mask]`.

  The `Upgrading` page carries the `TextEditingValue` move.
  - Test: the wiki guards, run against the branch.
- [ ] 18.2 The dashboard sample's forms screen shows a CPF, a phone, a postal code, a date with `ValidatingType`
  and an obscured PIN. If the README's capture of that screen changes, it is taken again
  (`./scripts/capture-readme.sh`), looked at, and recorded (`ReadmeCapturesTests`).
- [ ] 18.3 A ledger entry under `docs/ledger/` citing #1966.
  - Test: `LedgerTests`.
- [ ] 18.4 Archive the change with `openspec archive field-takes-the-shape-of-its-mask --yes` in the last of the
  change's pull requests, before it merges.
  - Check: `./scripts/check-openspec.sh`.
