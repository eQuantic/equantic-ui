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

[FormModel] public sealed class Patient { [Required, Mask(@"000\.000\.000-00")] public string Cpf { get; set; } = ""; }
```

- **`MaskedInput`** (Components): a `TextInput` that takes the shape of a mask written in .NET's language,
  read under the page's culture as .NET reads it (`.` `,` `:` `/` `$` are the culture's, so a CPF's dots are
  escaped, measured). It types, deletes and pastes as `MaskedTextBox` does, hands on a `MaskedText` (`Value`
  without literals, `Text` with them, `MaskCompleted`, `MaskFull`), opens the digits' keypad for a mask of
  digits, draws its shape as it is typed with the prompt shape as its placeholder, and says what it still asks
  for to assistive technology.
- **One mask engine**, written once in C# inside Components and transpiled into the runtime, held to
  `MaskedTextProvider` by a corpus measured from .NET, pinned, regenerated only under `EQ_UPDATE_MASK_CORPUS=1`,
  and executed under .NET and in the browser's twin.
- **`ITextInputFormatter`** (Primitives), Flutter's `TextInputFormatter` taking the edit rather than a second
  value, with **`TextEdit`** and **`TextEditKind`**, on **`TextEntry.Formatter`**; `TextInput` and `FormInput`
  take a `formatter`. `MaskFormatter` is the SDK's one; an app may write its own.
- **The web** cancels a formatted field's `beforeinput` and writes the formatter's text and caret (measured in
  Chrome 157 and Firefox 159), and never formats a composition while it is composed.
- **Photon** asks the formatter at every edit its host already funnels, a phone's whole value turned into one
  edit; iOS and Android open a non-prose field with no suggestions.
- **`[Mask]`** (Primitives), a DataAnnotations attribute the server checks with `MaskedTextProvider` itself, and
  which `[FormModel]` turns into the engine's rule in the browser.
- **`SemanticNode.Description`**: a field's helper, error and what a mask still asks for reach the screen
  readers on Photon (macOS, iOS, Android, Windows), as they reach the web's through `aria-describedby`.
- **eqc** carries `DateTimeFormatInfo.DateSeparator` and `TimeSeparator`, which the engine reads.
- **BREAKING (preview):** `eQuantic.UI.Native.Components.TextEditingValue` moves to `eQuantic.UI.Primitives`,
  unchanged. Migration line: name it from `eQuantic.UI.Primitives` (only a shell or a test names it).

## Capabilities

### New Capabilities

- `masked-input`: the mask language under the culture, the engine held to `MaskedTextProvider`, the editing of
  `MaskedTextBox`, the values handed on, the drawing, what it says to assistive technology, and server
  validation.
- `culture-separators`: a page reads its culture's date and time separators.

### Modified Capabilities

- `text-entry`: a formatter decides every edit, a composition is never formatted while composed, a phone's whole
  value reaches the formatter as one edit, a formatted entry is not prose.
- `phone-keyboard`: a formatted field asks the phone's keyboard for no suggestions.
- `native-accessibility`: a field's description reaches the native tree.

## Impact

- **Parts reached:** Primitives (vocabulary, attribute, semantics), Components (engine, `MaskFormatter`,
  `MaskedInput`, `TextInput`, `FormInput`, SdkStrings), the runtime (`lowering.ts`, node and enum twins, the
  transpiled modules), the web realizer (`WebLoweringVisitor.Text`), eqc (`PlatformMemberRead`), the Generators
  (`FormModelGenerator`), Native.Components (`PhotonHost`, `TextInputSession`, `SemanticsVisitor`), the iOS,
  Android, macOS and Windows shells (keyboard and accessibility bridges), the dashboard sample, the wiki. The
  SDKs and the templates are not reached.
- **Public surface moves:** `PublicAPI.Unshipped.txt` of Primitives (additions), Components (additions,
  `TextInput`'s and `FormInput`'s constructors widened) and Native.Components (`TextEditingValue`'s lines
  `*REMOVED*`). The developer surface (`developer-surface.baseline.txt`) does not move: no MSBuild property, no
  configuration section, no template parameter.
- **Principles followed:** 1, 2, 3, 4, 5, 6, 9, 13, 14, 15 (design.md says how). **Bent:** `MaskedTextBox`'s
  look (prompts drawn always) gives way to a placeholder, for assistive technology and because the web cannot
  draw a character that is not in the value; its behaviour, caret included, is kept.
- **Fenced out of 1.0:** overwrite mode, `PasswordChar` and `Obscure`, `ValidatingType`, undo (`MaskedTextBox`
  has none), automatic validation of server actions' arguments.
