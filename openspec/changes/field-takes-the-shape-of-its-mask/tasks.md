Pull request D, the last of the masked field's, which closes #1966. It needs A, B and C merged, and its server check rides on #1984. A section ends with the check that proves it against the real thing. Its heading names the sub-issue it closes. The shared design is `mask-engine-held-to-dotnet`'s, cited by decision number.

## 12. MaskedInput takes the shape of its mask, served and hydrated (5 points, #1979)

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

## 13. A masked field parses into a typed value, as the server does (5 points, #1980)

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

## 14. The server validates a masked value with MaskedTextProvider (3 points, #1981)

- [ ] 14.1 `[ServerOnly] MaskAttribute : ValidationAttribute` in Masking, with `AsciiOnly`, over .NET's
  `MaskedTextProvider` in the current culture.
  - Test: `MaskAttributeTests` through `Validator.TryValidateObject`, with the spec's four values in pt-BR and in
    the invariant culture.
- [ ] 14.2 `FormModelGenerator` reads `MaskAttribute` (by its namespace) and emits the engine's rule, with
  `ValidatingType` when it is given.
  - Test: the generator's snapshot test, and a conformance case running the generated rule in the browser with the
    same four values.
- [ ] 14.3 Check against the real thing: a server action in the dashboard that takes the model, called from the
  browser with a complete and an incomplete CPF (this rides on #1984 when it has merged, and on a hand call to `Validator` before).

## 17. Assistive technology hears what a masked field holds and what it still asks for (5 points, #1982)

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

## 18. The wiki and the dashboard show MaskedInput (3 points, #1983)

- [ ] 18.1 A wiki page, `MaskedInput`, in the five languages (English at the root, `locale/<culture>/` for pt-BR,
  es, fr and de), on a wiki branch named as the pull request's branch. It covers:
  - the mask language with the culture's separators (the CPF written `000\.000\.000-00`, and why);
  - the values handed on, overwrite, obscure, `ValidatingType`, undo, the keyboard, and `[Mask]`.
  - Test: the wiki guards, run against the branch.
- [ ] 18.2 The dashboard sample's forms screen shows a CPF, a phone, a postal code, a date with `ValidatingType`
  and an obscured PIN. If the README's capture of that screen changes, it is taken again
  (`./scripts/capture-readme.sh`), looked at, and recorded (`ReadmeCapturesTests`).
- [ ] 18.3 A ledger entry under `docs/ledger/` citing #1966.
  - Test: `LedgerTests`.
- [ ] 18.4 Archive the change with `openspec archive field-takes-the-shape-of-its-mask --yes` before its pull
  request merges, after the three changes before it are archived: it MODIFIES the mask-engine requirement
  `mask-engine-held-to-dotnet` adds.
  - Check: `./scripts/check-openspec.sh`.
