## Why

This change ships pull request A of #1966, a Feature under the epic #898 (milestone 1.0.0), through sub-issues
#1968 to #1974. A field that takes the shape of a mask needs, before any field, an engine that answers what .NET's
`System.ComponentModel.MaskedTextProvider` answers, written once and run on both targets. It also needs a way for
every host to hand an edit to it. This change is the first of five, and its design is the shared design of all
five.

## What Changes

For a developer:

```csharp
var cpf = new MaskFormatter(@"000\.000\.000-00");
var text = cpf.Fit("12345678901");            // "123.456.789-01"
MaskedText read = cpf.Read(text);             // Value "12345678901", MaskCompleted true
TextInput(value, onChanged, label: "CPF", formatter: cpf)   // a field the hosts start to honour in B and C
```

- **`eQuantic.UI.Masking`**, a new assembly and package, delivered by both SDKs with the namespace in scope, so the
  csproj names nothing.
  - It holds `MaskFormatter`, `MaskedText` and `InsertKeyMode`.
  - Its engine answers what `MaskedTextProvider` and `MaskedTextBox`'s editing answer: insert, overwrite, delete,
    paste, and a history.
  - It is transpiled into the runtime.
- **A corpus measured from .NET**, pinned, regenerated only under `EQ_UPDATE_MASK_CORPUS=1`, and executed under
  .NET and in the browser's twin.
- **eqc carries `DateTimeFormatInfo.DateSeparator` and `TimeSeparator`**, which the engine reads.
- **The vocabulary** (Primitives):
  - `ITextInputFormatter`, `TextEdit`, `TextEditKind`, `TextCommand`, and `TextEntry.Formatter`;
  - `TextEditingValue`, moved here;
  - a `formatter` on `TextInput` and `FormInput`.
  - No host asks the formatter yet: B and C make the web and Photon do so.
- **BREAKING (preview):** `eQuantic.UI.Native.Components.TextEditingValue` moves to `eQuantic.UI.Primitives`,
  unchanged. Migration line: name it from `eQuantic.UI.Primitives`. Only a shell or a test names it.

## Capabilities

### New Capabilities

- `mask-engine`: the mask language under the culture, the engine held to `MaskedTextProvider`, `MaskedTextBox`'s
  editing, overwrite, the history, the values read from a text, and the assembly the SDK delivers.
- `culture-separators`: a page reads its culture's date and time separators.

### Modified Capabilities

- `text-entry`: a text entry carries a formatter, and the input family hands it on.

## Impact

- **Parts reached:**
  - the new `eQuantic.UI.Masking`;
  - Primitives: the vocabulary;
  - Components: `TextInput` and `FormInput`;
  - both SDKs' `Sdk.props`;
  - eqc: `PlatformMemberRead`, `RuntimeProvidedTypeScanner`;
  - Web.Build: the enum unions;
  - the runtime: the node and enum twins, the transpiled modules;
  - Native.Components: `TextEditingValue`'s uses;
  - the conformance tests and the solution.
  - The web realizer, the Photon shells and the templates are not reached.
- **Public surface moves:**
  - a new `PublicAPI.Unshipped.txt` for Masking;
  - additions in Primitives' and Components';
  - `TextEditingValue`'s lines `*REMOVED*` in Native.Components'.
  - `developer-surface.baseline.txt` does not move: a package reference and a using, and no property.
- **Principles:** 1, 3, 4, 5, 6, 9, 13 and 14 are followed, as the shared design says. None is bent here.
