## ADDED Requirements

### Requirement: A formatted field asks the phone's keyboard for no suggestions

The iOS and Android shells SHALL open the keyboard of a field whose formatter is not prose without
autocorrection, suggestions or automatic capitals (`UITextAutocorrectionType.No`,
`UITextSpellCheckingType.No` and `UITextAutocapitalizationType.None` on iOS,
`TYPE_TEXT_FLAG_NO_SUGGESTIONS` on Android), whatever its kind, so a keyboard commits each key rather than
composing a word.

#### Scenario: A plate's keyboard

- **WHEN** the caret enters a `MaskedInput` of mask `>LL-0000` on the iOS simulator and on the Android emulator
- **THEN** the text keyboard opens with no suggestion bar and no capital forced, and `a` typed reaches the field
  as `A`

#### Scenario: A postal code's keypad

- **WHEN** the caret enters a `MaskedInput` of mask `00000-000` on the same two
- **THEN** iOS opens `NumberPad` and Android describes `ClassNumber`
