## ADDED Requirements

### Requirement: A mask is read in .NET's language, under the page's culture

A `MaskFormatter` and a `MaskedInput` SHALL read a mask as `System.ComponentModel.MaskedTextProvider` reads
it, with `MaskedTextBox`'s syntax: `0` a required digit, `9` an optional digit or space, `#` an optional
digit, space or sign, `L` and `?` a required and an optional letter, `&` and `C` a required and an optional
character, `A` and `a` a required and an optional letter or digit, `<`, `>` and `|` the case conversions,
`\` an escape, and `.`, `,`, `:`, `/` and `$` as the current culture's decimal separator, group separator,
time separator, date separator and currency symbol. Every other character SHALL be a literal.

#### Scenario: A CPF written with dots, in Brazil

- **WHEN** a field of mask `000.000.000-00` is drawn empty in pt-BR, and `12345678901` is typed into it
- **THEN** its placeholder is `___,___,___-__` and its text `123,456,789-01`, as .NET 10's
  `MaskedTextProvider` writes them

#### Scenario: A CPF with escaped dots

- **WHEN** `12345678901` is typed into a field of mask `000\.000\.000-00` in pt-BR
- **THEN** its text is `123.456.789-01`

#### Scenario: The culture's date separator

- **WHEN** `28112026` is typed into a field of mask `00/00/0000` in pt-BR and in de-DE
- **THEN** pt-BR shows `28/11/2026` and de-DE `28.11.2026`

#### Scenario: The culture's currency and separators

- **WHEN** `12345678` is typed into a field of mask `$999,999.00` in en-US, pt-BR and de-DE
- **THEN** they show `$123,456.78`, `R$123.456,78` and `€123.456,78`

#### Scenario: Case conversion

- **WHEN** `ab1234` is typed into a field of mask `>LL-0000`
- **THEN** it shows `AB-1234`

### Requirement: The mask engine answers what MaskedTextProvider answers

The SDK's mask engine SHALL leave, after each of `Add`, `InsertAt`, `Replace` (a character or a string, at a
position or over a range), `RemoveAt` (a position or a range), `Remove`, `Set` and `Clear`, the state .NET's
`MaskedTextProvider` leaves for the same mask, culture and options: the result, the test position, the hint,
`ToString` with and without the prompt and the literals, `ToDisplayString`, `MaskCompleted`, `MaskFull`, the
assigned and available counts and `LastAssignedPosition`; and SHALL answer `IsEditPosition` and the three
`Find…EditPositionFrom` in both directions as .NET does, on .NET and in the browser. The cases SHALL be a
corpus generated from .NET's class, compared on every run and rewritten only under
`EQ_UPDATE_MASK_CORPUS=1`.

#### Scenario: Removing a literal

- **WHEN** `RemoveAt(3)` is called on a pt-BR CPF holding `123,456,789-01`
- **THEN** it answers true with the hint `NoEffect` and the text unchanged, on .NET and in the browser

#### Scenario: Removing a digit shifts what follows

- **WHEN** `RemoveAt(2)` is called on the same CPF
- **THEN** it holds `124,567,890-1_` and `MaskCompleted` is false

#### Scenario: An optional mask is complete empty

- **WHEN** a field of mask `(99) 9999-9999` holds nothing
- **THEN** `MaskCompleted` is true and `MaskFull` false

#### Scenario: Any decimal digit fills a digit

- **WHEN** `１` (U+FF11) is added to mask `00000-000` with `AsciiOnly` on, and `é` to mask `LLLL` with it on
- **THEN** the first is placed, and the second is refused with `AsciiCharacterExpected`

#### Scenario: The corpus moved

- **WHEN** a case of the pinned corpus differs from what .NET's class answers now, without `EQ_UPDATE_MASK_CORPUS`
- **THEN** the corpus test fails naming the case, and the file is not rewritten

### Requirement: A masked field edits as MaskedTextBox edits

A `MaskedInput` SHALL type, delete and paste as `MaskedTextBox` does in its default insert mode, with
`RejectInputOnFirstFailure` off: a typed character goes to the next edit position and the caret to the
position after it; a Backspace or a Delete removes the edit positions of the selection, or of the one
position before or after the caret, and shifts what follows, and when it removes nothing the caret crosses
the literal in the key's direction; a paste or a replacement places each character in turn and skips one the
mask refuses; a field's text is never left holding a character the mask refuses.

#### Scenario: The caret skips a literal

- **WHEN** `1`, `2`, `3` and `4` are typed into an empty pt-BR CPF
- **THEN** the caret stands at 3 after the `3`, and the `4` lands after the separator with the caret at 5

#### Scenario: Backspace over a literal

- **WHEN** Backspace is pressed with the caret at 4 of a full pt-BR CPF, and again
- **THEN** the first leaves the text and puts the caret at 3, and the second removes `3` and leaves
  `124,567,890-1` with the caret at 2

#### Scenario: Delete at a literal

- **WHEN** Delete is pressed with the caret at 3 of a full pt-BR CPF
- **THEN** the text is unchanged and the caret moves to 4

#### Scenario: A formatted paste

- **WHEN** `(11) 98765-4321` is pasted over the whole of a field of mask `(00) 00000-0000`
- **THEN** it holds `(11) 98765-4321`, the mask is completed, and the caret is at the end

#### Scenario: A paste the mask cannot hold whole

- **WHEN** `1234567890199` is pasted into an empty pt-BR CPF
- **THEN** it holds `123,456,789-01` and the last two characters are dropped

#### Scenario: A refused character

- **WHEN** `x` is typed into a pt-BR CPF at an edit position
- **THEN** the text and the caret are unchanged

### Requirement: A masked field hands on its raw value, its text and its completeness

`MaskedInput` SHALL take its `value` as `MaskedTextBox.Text` is set, so the raw value and the formatted text
both fill it, and SHALL hand `onChanged` a `MaskedText` holding `Value` (no literals, no prompts), `Text`
(with literals), `MaskCompleted` and `MaskFull`, after every edit that changed the text. A re-render whose
value fits to the text the field holds SHALL leave the text and the caret where they are.

#### Scenario: Raw in, both out

- **WHEN** a pt-BR CPF field is given `12345678901`, and its last digit is deleted
- **THEN** it hands on `Value` `1234567890`, `Text` `123,456,789-0`, `MaskCompleted` false

#### Scenario: A parent that rebuilds on every key

- **WHEN** a parent stores each `Value` handed on and builds the field again with it, while `123` is typed
- **THEN** the caret stays where the typing left it after each key

#### Scenario: An optional gap survives the round trip

- **WHEN** a field of mask `(99) 9999-9999` holding `(1_) 2345-678_` hands on its value and is built again with it
- **THEN** it shows the same text, the gap included

### Requirement: A masked field draws what it holds and shows its shape as a placeholder

A `MaskedInput` SHALL draw its text with the literals and without prompts, cut after the last assigned
position, with a space for an unassigned position before it; empty, it SHALL show the mask's prompt shape
as its placeholder, unless the app gives one. The entry SHALL be of kind `Number` when every edit position
is `0` or `9`, and `Text` otherwise, and SHALL NOT be treated as prose.

#### Scenario: The shape grows

- **WHEN** `1234` is typed into an empty pt-BR CPF
- **THEN** the field draws `123,4`

#### Scenario: The digits' keypad

- **WHEN** a field of mask `00000-000` and one of mask `>LL-0000` are drawn
- **THEN** the first entry is of kind `Number` and the second of kind `Text`

### Requirement: A masked field says what it still asks for

A `MaskedInput` SHALL give its entry a description that says how many required positions are still empty
while the mask is not completed, after the helper or the error, and SHALL say nothing of it once the mask is
completed. A field left holding text that does not complete the mask SHALL show the SDK's incomplete error,
unless the app gives its own, and SHALL NOT show it while it is being typed into.

#### Scenario: Three to go

- **WHEN** `12345678` is typed into a pt-BR CPF field with no helper
- **THEN** its entry's description says three more characters are required, in the page's culture

#### Scenario: Left incomplete

- **WHEN** the same field is left
- **THEN** it shows the incomplete error and its entry is invalid; it showed neither while typed into

### Requirement: The server validates a masked value with MaskedTextProvider

A property marked `[Mask("…")]` SHALL be valid under `System.ComponentModel.DataAnnotations.Validator` when it
is null or empty, or when .NET's `MaskedTextProvider`, in the current culture with the SDK's options, places
every character of it and completes the mask. A `[FormModel]` with such a property SHALL validate it in the
browser with the SDK's engine, by the same rule.

#### Scenario: Raw and formatted on the server

- **WHEN** a model's `[Mask("000.000.000-00")]` property holds `12345678901`, `123,456,789-01`, `1234567890`
  and `123.456.789-01`, validated in pt-BR
- **THEN** the first two are valid and the last two are not

#### Scenario: The same rule in the browser

- **WHEN** a `[FormModel]` with that property is validated in the browser in pt-BR with the same four values
- **THEN** it answers as the server does
