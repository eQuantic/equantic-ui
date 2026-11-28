## Purpose

`MaskedInput`, the field that takes the shape of a mask on the web and on Photon: the values it hands on, how it draws, overtypes, obscures, parses, undoes and is served and hydrated, what it says to assistive technology, and how the server validates a masked value with `MaskedTextProvider`.

## ADDED Requirements

### Requirement: A masked field takes the shape of its mask on every target

A `MaskedInput` SHALL be a `TextInput` whose entry is formatted by one `MaskFormatter` of its mask, kept across the
parent's rebuilds, so it types, deletes, pastes, overtypes and undoes as the mask engine does, on the web and on
Photon.

#### Scenario: A CPF typed on both targets

- **WHEN** `12345678901` is typed into a pt-BR `MaskedInput` of mask `000\.000\.000-00`, in Chrome and in the
  PhotonDesktop sample
- **THEN** both show `123.456.789-01`, with the caret at the end

### Requirement: A masked field hands on its raw value, its text and its completeness

`MaskedInput` SHALL take its `value` as `MaskedTextBox.Text` is set, so the raw value and the formatted text both
fill it. After every edit that changed the text, it SHALL hand `onChanged` the `MaskedText` its formatter reads.
A re-render whose value fits to the text the field holds SHALL leave the text and the caret where they are.

#### Scenario: Raw in, both out

- **WHEN** a pt-BR CPF field is given `12345678901`, and its last digit is deleted
- **THEN** it hands on `Value` `1234567890`, `Text` `123,456,789-0`, `MaskCompleted` false

#### Scenario: A parent that rebuilds on every key

- **WHEN** a parent stores each `Value` handed on and builds the field again with it, while `123` is typed
- **THEN** the caret stays where the typing left it after each key

### Requirement: A masked field draws what it holds and shows its shape as a placeholder

A `MaskedInput` SHALL draw the text its formatter writes. When empty, it SHALL show the mask's prompt shape as its
placeholder, unless the app gives one. The entry SHALL be of kind `Number` when every edit position is `0` or
`9`, and `Text` otherwise.

#### Scenario: The shape grows

- **WHEN** `1234` is typed into an empty pt-BR CPF field of mask `000.000.000-00`
- **THEN** the field draws `123,4`, and drew `___,___,___-__` as its placeholder before

#### Scenario: The digits' keypad

- **WHEN** a field of mask `00000-000` and one of mask `>LL-0000` are drawn
- **THEN** the first entry is of kind `Number` and the second of kind `Text`

### Requirement: A masked field overtypes with the Insert key

A `MaskedInput` SHALL take an `InsertKeyMode`, handed to its formatter.

#### Scenario: The Insert key

- **WHEN** Insert is pressed in a field of `InsertKeyMode.Default`, then `9` is typed at 0 of a full CPF
- **THEN** the `9` replaces the first digit, and Insert pressed again goes back to inserting

### Requirement: An obscured masked field hides what was entered and shows its literals

A `MaskedInput` with `Obscure` SHALL draw every entered position as the target's password glyph and every literal
as it is, as `MaskedTextBox` with a `PasswordChar` draws it. It SHALL keep everything the target's own password
field gives:

- no copy out;
- no autocorrection;
- the secure keyboard on iOS and Android;
- on the web, a real `type="password"` input that password managers recognise.

#### Scenario: A PIN with a separator

- **WHEN** an obscured field of mask `0000-0000` holds `12345`, in Chrome, in Firefox and on Photon
- **THEN** the web's input is of type `password` and holds `1234-5` with `-` drawn over its fifth cell, and Photon
  draws `••••-•`

### Requirement: A masked field parses its text into a typed value

A `MaskedInput` SHALL take a `validatingType`, an `IMaskedValueParser`.

- A `MaskedValueParser<TValue>` SHALL be built from a method of the shape
  `bool TryParse(string, IFormatProvider?, out TValue)`.
- It SHALL parse a `MaskedText`'s `Text` in the current culture.
- A field left completed but unparsable SHALL show the SDK's invalid value error, as `MaskedTextBox`'s
  `TypeValidationCompleted` reports `IsValidInput` false.

#### Scenario: A date in Brazil

- **WHEN** `28112026` and then `31022026` are typed into a pt-BR field of mask `00/00/0000` with
  `new MaskedValueParser<DateOnly>(DateOnly.TryParse)`, and the field is left after each
- **THEN** the first parses to 2026-11-28 and the field shows no error, and the second shows the invalid value
  error, on .NET and in the browser

### Requirement: A masked field undoes and redoes, and a reset is not undone

A `MaskedInput` SHALL show the step its formatter's history answers on undo and redo, and hand the page its
`MaskedText`. A value the page writes SHALL clear the history.

#### Scenario: A reset is not undone

- **WHEN** `123` is typed, the page resets the value to `""`, and undo is pressed
- **THEN** the field stays empty

### Requirement: A masked field says what it still asks for

While its mask is not completed, a `MaskedInput` SHALL give its entry a description, after the helper or the
error, that says how many required positions are still empty. It SHALL say nothing of it once the mask is
completed. A field left holding text that does not complete the mask SHALL show the SDK's incomplete error,
unless the app gives its own, and SHALL NOT show it while it is being typed into.

#### Scenario: Three to go

- **WHEN** `12345678` is typed into a pt-BR CPF field with no helper
- **THEN** its entry's description says three more characters are required, in the page's culture

#### Scenario: Left incomplete

- **WHEN** the same field is left
- **THEN** it shows the incomplete error and its entry is invalid; it showed neither while typed into

### Requirement: A masked field is served and hydrated as the browser draws it

The server SHALL draw a `MaskedInput` with the C# engine as the browser draws it with the twin, and hydration
SHALL leave a field nobody used as it was served. When it hydrates, a field typed into before the runtime loaded
SHALL be fitted once, as a paste over its whole text, with the caret where the fit ends.

#### Scenario: Typed before the runtime loaded

- **WHEN** `12345` is typed into a served pt-BR CPF field before hydration
- **THEN** after hydration it shows `123,45` with the caret at 6, and the page has heard it

### Requirement: The server validates a masked value with MaskedTextProvider

A property marked `[Mask("…")]` (`eQuantic.UI.Masking.MaskAttribute`) SHALL be valid under
`System.ComponentModel.DataAnnotations.Validator` when it is null or empty. Otherwise it SHALL be valid when .NET's
`MaskedTextProvider`, in the current culture with the SDK's options, places every character of it and completes
the mask, and, when the attribute names a `ValidatingType`, the value parses with that type's
`TryParse(string, IFormatProvider, out T)` in the current culture. A `[FormModel]` with such a property SHALL
validate it in the browser with the SDK's engine, by the same rule.

#### Scenario: Raw and formatted on the server

- **WHEN** a model's `[Mask("000.000.000-00")]` property holds `12345678901`, `123,456,789-01`, `1234567890`
  and `123.456.789-01`, validated in pt-BR
- **THEN** the first two are valid and the last two are not

#### Scenario: The same rule in the browser

- **WHEN** a `[FormModel]` with that property is validated in the browser in pt-BR with the same four values
- **THEN** it answers as the server does

#### Scenario: A typed value on the server

- **WHEN** a model's `[Mask("00/00/0000", ValidatingType = typeof(DateOnly))]` property holds `28/11/2026` and
  `31/02/2026`, validated in pt-BR
- **THEN** the first is valid and the second is not
