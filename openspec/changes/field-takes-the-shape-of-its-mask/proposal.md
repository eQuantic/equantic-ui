## Why

Closes #1966, a Feature under the epic #898 (milestone 1.0.0), through sub-issues #1979 to #1983. With the engine
(`mask-engine-held-to-dotnet`) and the field edits on both targets (`a-formatter-decides-a-web-fields-edits`,
`a-formatter-decides-a-photon-fields-edits`) shipped, the component a developer writes is what is left. So is the
typed value, the server's check of a masked value, and what assistive technology hears. A CPF, a phone number, a
postal code, a plate or a card's expiry no longer needs an app's own handlers on each target.

## What Changes

For a developer:

```csharp
MaskedInput(mask: @"000\.000\.000-00", value: cpf, onChanged: v => SetState(() => cpf = v.Value), label: "CPF")
MaskedInput(mask: "00/00/0000", value: day, validatingType: Days, insertKeyMode: InsertKeyMode.Overwrite, onChanged: …)
MaskedInput(mask: "0000-0000", value: pin, onChanged: …) { Obscure = true }
[FormModel] public sealed class Patient { [Required, Mask(@"000\.000\.000-00")] public string Cpf { get; set; } = ""; }
```

- **`MaskedInput`** (Components) is a `TextInput` formatted by its mask.
  - It hands on `MaskedText`, draws its shape as it is typed with the prompt shape as placeholder, and opens the
    digits' keypad for a mask of digits.
  - It overtypes with `InsertKeyMode`, obscures with its literals visible, undoes and redoes, and is served and
    hydrated.
- **`IMaskedValueParser` and `MaskedValueParser<TValue>`** (Masking): `ValidatingType` as a value,
  `new MaskedValueParser<DateOnly>(DateOnly.TryParse)`.
- **`[Mask]`** (Masking, server only): DataAnnotations over .NET's `MaskedTextProvider` itself, with
  `ValidatingType`. `[FormModel]` turns it into the engine's rule in the browser.
- **`SemanticNode.Description`**: a field's helper, error and what a mask still asks for reach the screen readers on
  Photon. The web already carries them through `aria-describedby`.
- **No break.**

## Capabilities

### New Capabilities

- `masked-input`: the component on every target, the values handed on, the drawing, overwrite, obscure, the typed
  value, undo, what it says to assistive technology, SSR and hydration, and server validation of a masked value.

### Modified Capabilities

- `mask-engine`: the assembly's surface gains the parsers and `MaskAttribute`.
- `native-accessibility`: a field's description reaches the native tree.

## Impact

- **Parts reached:**
  - Components: `MaskedInput`, SdkStrings;
  - Masking: the parsers, `MaskAttribute`;
  - Primitives: `SemanticNode`;
  - the Generators: `FormModelGenerator`;
  - Native.Components: `SemanticsVisitor`, and the four shells' accessibility bridges;
  - the dashboard sample;
  - the wiki's `MaskedInput` page, in five languages.
- **Public surface moves:** additions in Components', Masking's and Primitives' `PublicAPI.Unshipped.txt`. The
  developer surface does not move.
- **Principles:** 1, 2, 3, 4, 10 and 15 are followed. One is bent: `MaskedTextBox`'s look (prompts always drawn)
  gives way to a placeholder, as the shared design says.
