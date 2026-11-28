## Why

Closes #1966, a Feature under the epic #898 (the exit from preview, milestone 1.0.0). A field cannot take the
shape of a mask today: a CPF, a phone number, a postal code, a plate or a card's expiry is written by each app
in its own handlers, on each target separately, or not at all. The general capability under the request is a
field whose edits pass through a rule before they land, and .NET already has the rule's language for masks:
`System.ComponentModel.MaskedTextProvider`, with `MaskedTextBox`'s syntax, which the owner chose.

## What Changes

For a developer:

```csharp
MaskedInput(mask: @"000\.000\.000-00", value: cpf, onChanged: v => SetState(() => cpf = v.Value), label: "CPF")
MaskedInput(mask: "00/00/0000", value: day, validatingType: Days, insertKeyMode: InsertKeyMode.Overwrite, onChanged: …)
MaskedInput(mask: "0000-0000", value: pin, onChanged: …) { Obscure = true }

[FormModel] public sealed class Patient { [Required, Mask(@"000\.000\.000-00")] public string Cpf { get; set; } = ""; }
[ServerAction] public Task Save(Patient patient) => …;   // refused with the field's message before it runs
```

- **`MaskedInput`** (Components): a `TextInput` that takes the shape of a mask written in .NET's language.
  - The mask is read under the page's culture, as .NET reads it: `.` `,` `:` `/` `$` are the culture's, so a
    CPF's dots are escaped (measured).
  - It types, deletes, pastes and overtypes as `MaskedTextBox` does, with `InsertKeyMode` and the Insert key.
  - It hands on a `MaskedText`: `Value` without literals, `Text` with them, `MaskCompleted`, `MaskFull`.
  - It parses into a typed value with `validatingType` (`MaskedValueParser<T>(T.TryParse)`), as
    `ValidatingType` does.
  - It undoes and redoes from every target's own keys, menu and gestures.
  - With `Obscure`, it obscures the entered positions and shows the literals, keeping a real password field
    underneath.
  - It opens the digits' keypad for a mask of digits, draws its shape as it is typed with the prompt shape as its
    placeholder, and says what it still asks for to assistive technology.
- **`eQuantic.UI.Masking`**, a new assembly and package, delivered by both SDKs with the namespace in scope. It is
  the mask engine, written once in C# and transpiled into the runtime, held to `MaskedTextProvider` by a corpus
  measured from .NET. The corpus is pinned, regenerated only under `EQ_UPDATE_MASK_CORPUS=1`, and executed under
  .NET and in the browser's twin.
- **`ITextInputFormatter`** (Primitives): Flutter's `TextInputFormatter`, taking the edit rather than a second
  value, plus the commands a host routes to it.
  - It comes with **`TextEdit`**, **`TextEditKind`** and **`TextCommand`**, on **`TextEntry.Formatter`**.
  - `TextInput` and `FormInput` take a `formatter`.
- **The web** cancels a formatted field's `beforeinput` and writes the formatter's text and caret through
  `execCommand`. That keeps the browser's menus and gestures dispatching undo (measured in Chrome 157 and
  Firefox 159). It never formats a composition while it is composed.
- **Photon** asks the formatter at every edit and command its host already funnels, a phone's whole value turned
  into one edit.
  - iOS and Android open a non-prose field with no suggestions.
  - macOS gains Edit › Undo and Redo.
  - iOS's shake and three-finger gestures reach the formatter through an undo manager.
- **`[Mask]`**: a DataAnnotations attribute the server checks with `MaskedTextProvider` itself, and which
  `[FormModel]` turns into the engine's rule in the browser.
- **Server actions validate their arguments' DataAnnotations** before they run, nested models included.
  - A refusal is `400` with .NET's `HttpValidationProblemDetails`.
  - The browser throws `ServerValidationException`, and `FormController.SubmitAsync` puts its messages on the
    fields. The developer writes nothing.
- **`SemanticNode.Description`**: a field's helper, error and what a mask still asks for reach the screen readers
  on Photon (macOS, iOS, Android, Windows), as they reach the web's through `aria-describedby`.
- **eqc** carries `DateTimeFormatInfo.DateSeparator` and `TimeSeparator`, which the engine reads.
- **BREAKING (preview):**
  - `eQuantic.UI.Native.Components.TextEditingValue` moves to `eQuantic.UI.Primitives`, unchanged. Migration
    line: name it from `eQuantic.UI.Primitives`.
  - A server action whose arguments carry DataAnnotations now refuses invalid ones before it runs. Migration line:
    drop hand-written validation, or take a type without the attributes where an invalid model must be accepted.

## Capabilities

### New Capabilities

- `masked-input`: covers:
  - the mask language under the culture;
  - the engine's assembly, and the engine held to `MaskedTextProvider`;
  - the editing of `MaskedTextBox`: overwrite, obscure, the typed value, undo;
  - the values handed on, the drawing, and what it says to assistive technology;
  - server validation of a masked value.
- `culture-separators`: a page reads its culture's date and time separators.
- `server-action-validation`: a Server Action validates its arguments' DataAnnotations before it runs, and the page
  and its form hear the verdict.

### Modified Capabilities

- `text-entry`:
  - a formatter decides every edit;
  - a composition is never formatted while composed;
  - a phone's whole value reaches the formatter as one edit;
  - a formatted entry is not prose;
  - the targets' undo, redo and Insert reach the formatter;
  - an obscured formatted entry shows its literals.
- `phone-keyboard`: a formatted field asks the phone's keyboard for no suggestions.
- `native-accessibility`: a field's description reaches the native tree.

## Impact

- **Parts reached:**
  - Primitives: vocabulary, semantics, `ServerValidationException`, `FormController`.
  - The new `eQuantic.UI.Masking`: engine, formatter, parsers, attribute.
  - Components: `MaskedInput`, `TextInput`, `FormInput`, SdkStrings.
  - Both SDKs' `Sdk.props`.
  - eqc: `PlatformMemberRead`, `RuntimeProvidedTypeScanner`.
  - Web.Build: the enum unions.
  - The runtime: `lowering.ts`, `server-actions.ts`, the node and enum twins, the transpiled modules.
  - The web realizer: `WebLoweringVisitor.Text`.
  - The Server: `ServerActionsMiddleware` and the argument validator.
  - The Generators: `FormModelGenerator`.
  - Native.Components: `PhotonHost`, `TextInputSession`, `EmitVisitor`, `SemanticsVisitor`.
  - The iOS, Android, macOS and Windows shells: keyboard, menu, undo manager, accessibility bridges.
  - The dashboard sample and the wiki.
  - The templates are not reached.
- **Public surface moves:**
  - A new `PublicAPI.Unshipped.txt` for Masking.
  - `PublicAPI.Unshipped.txt` of Primitives (additions), Components (additions, `TextInput`'s and `FormInput`'s
    constructors widened) and Native.Components (`TextEditingValue`'s lines `*REMOVED*`).
  - The developer surface (`developer-surface.baseline.txt`) does not move: the SDKs gain a package reference and
    a using, and no MSBuild property, configuration section or template parameter.
- **Principles followed:** 1, 2, 3, 4, 5, 6, 9, 13, 14, 15 (design.md says how).
- **Principles bent:**
  - `MaskedTextBox`'s look (prompts drawn always) gives way to a placeholder.
  - On the web, an obscured masked field spaces its literals as bullets.
  - Server-action validation walks .NET's `Validator` itself, because .NET 10's `Microsoft.Extensions.Validation`
    host API is experimental (ASP0029, measured).
