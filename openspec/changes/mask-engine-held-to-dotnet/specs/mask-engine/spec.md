## Purpose

The SDK's mask engine, `eQuantic.UI.Masking`: a mask read in .NET's `MaskedTextProvider` language under the page's culture, every operation answered as .NET's class answers it, `MaskedTextBox`'s editing (insert, overwrite, delete, paste) and a history, written once and held to .NET by a corpus measured from the real class.

## ADDED Requirements

### Requirement: A mask is read in .NET's language, under the page's culture

A `MaskFormatter` SHALL read a mask as `System.ComponentModel.MaskedTextProvider` reads it, with `MaskedTextBox`'s
syntax:

- `0` a required digit, and `9` an optional digit or space;
- `#` an optional digit, space or sign;
- `L` and `?` a required and an optional letter;
- `&` and `C` a required and an optional character;
- `A` and `a` a required and an optional letter or digit;
- `<`, `>` and `|` the case conversions, and `\` an escape;
- `.`, `,`, `:`, `/` and `$` as the current culture's decimal separator, group separator, time separator, date
  separator and currency symbol.

Every other character SHALL be a literal.

#### Scenario: A CPF written with dots, in Brazil

- **WHEN** `12345678901` is typed through a `MaskFormatter` of mask `000.000.000-00` in pt-BR
- **THEN** its text is `123,456,789-01`, as .NET 10's `MaskedTextProvider` writes it

#### Scenario: A CPF with escaped dots

- **WHEN** `12345678901` is typed through a `MaskFormatter` of mask `000\.000\.000-00` in pt-BR
- **THEN** its text is `123.456.789-01`

#### Scenario: The culture's date separator

- **WHEN** `28112026` is typed through a `MaskFormatter` of mask `00/00/0000` in pt-BR and in de-DE
- **THEN** pt-BR gives `28/11/2026` and de-DE `28.11.2026`

#### Scenario: The culture's currency and separators

- **WHEN** `12345678` is typed through a `MaskFormatter` of mask `$999,999.00` in en-US, pt-BR and de-DE
- **THEN** they give `$123,456.78`, `R$123.456,78` and `€123.456,78`

#### Scenario: Case conversion

- **WHEN** `ab1234` is typed through a `MaskFormatter` of mask `>LL-0000`
- **THEN** it gives `AB-1234`

### Requirement: The mask engine answers what MaskedTextProvider answers

The SDK's mask engine SHALL leave, after each of these operations, the state .NET's `MaskedTextProvider` leaves for
the same mask, culture and options: `Add`, `InsertAt`, `Replace` (a character or a string, at a position or over a
range), `RemoveAt` (a position or a range), `Remove`, `Set` and `Clear`.

The state compared is:

- the result, the test position and the hint;
- `ToString` with and without the prompt and the literals, and with the password character ignored;
- `ToDisplayString`, `MaskCompleted` and `MaskFull`;
- the assigned and available counts, and `LastAssignedPosition`.

It SHALL answer `IsEditPosition` and the three `Find…EditPositionFrom` methods in both directions as .NET does,
on .NET and in the browser. The cases SHALL be a corpus generated from .NET's class, compared on every run, and
rewritten only under `EQ_UPDATE_MASK_CORPUS=1`.

#### Scenario: Removing a literal

- **WHEN** `RemoveAt(3)` is called on a pt-BR CPF holding `123,456,789-01`
- **THEN** it answers true with the hint `NoEffect` and the text unchanged, on .NET and in the browser

#### Scenario: Removing a digit shifts what follows

- **WHEN** `RemoveAt(2)` is called on the same CPF
- **THEN** it holds `124,567,890-1_` and `MaskCompleted` is false

#### Scenario: An optional mask is complete empty

- **WHEN** a mask `(99) 9999-9999` holds nothing
- **THEN** `MaskCompleted` is true and `MaskFull` false

#### Scenario: Any decimal digit fills a digit

- **WHEN** `１` (U+FF11) is added to mask `00000-000` with `AsciiOnly` on, and `é` to mask `LLLL` with it on
- **THEN** the first is placed, and the second is refused with `AsciiCharacterExpected`

#### Scenario: A password character

- **WHEN** a mask `0000-0000` with the password character `*` holds `12345`
- **THEN** `ToDisplayString` is `****-*___`

#### Scenario: The corpus moved

- **WHEN** a case of the pinned corpus differs from what .NET's class answers now, without `EQ_UPDATE_MASK_CORPUS`
- **THEN** the corpus test fails naming the case, and the file is not rewritten

### Requirement: A mask edits as MaskedTextBox edits

A `MaskFormatter` SHALL answer each `TextEdit` as `MaskedTextBox` edits in its default insert mode, with
`RejectInputOnFirstFailure` off:

- A typed character goes to the next edit position, and the caret to the position after it.
- A `DeletedBackward` or `DeletedForward` edit removes the edit positions of its range, and shifts what follows.
  When it removes nothing, the caret crosses the literal in the edit's direction.
- A `Pasted` edit places each character in turn, and skips one the mask refuses.

The text it answers SHALL never hold a character the mask refuses.

#### Scenario: The caret skips a literal

- **WHEN** `1`, `2`, `3` and `4` are typed, one edit each, into an empty pt-BR CPF
- **THEN** the caret stands at 3 after the `3`, and the `4` lands after the separator with the caret at 5

#### Scenario: Backspace over a literal

- **WHEN** a `DeletedBackward` edit over position 3 is handed with the caret at 4 of a full pt-BR CPF, and then one
  over position 2
- **THEN** the first leaves the text and puts the caret at 3, and the second leaves `124,567,890-1` with the caret
  at 2

#### Scenario: Delete at a literal

- **WHEN** a `DeletedForward` edit over position 3 is handed to a full pt-BR CPF
- **THEN** the text is unchanged and the caret moves to 4

#### Scenario: A paste the mask cannot hold whole

- **WHEN** `1234567890199` is pasted into an empty pt-BR CPF
- **THEN** it holds `123,456,789-01` and the last two characters are dropped

#### Scenario: A paste with spaces

- **WHEN** `+55 11 98765-4321` is pasted over the whole of mask `(00) 00000-0000`
- **THEN** it holds `(55) _11_9-8765`, as .NET's provider places it, a space emptying a position

### Requirement: A mask overtypes as MaskedTextBox does

A `MaskFormatter` SHALL take an `InsertKeyMode` with .NET's three members:

- `Default` inserts, and `TextCommand.ToggleOverwrite` switches it to overwrite and back;
- `Insert` always inserts, and refuses the command;
- `Overwrite` always overwrites, and refuses the command.

In overwrite mode a typed character SHALL replace the edit position at or after the caret without shifting what
follows, as `MaskedTextBox`'s `PlaceChar` with `overwrite` does, and `Overwriting` SHALL say so.

#### Scenario: Overtyping a full CPF

- **WHEN** `9` is typed at 0 and `8` at 3 of a full pt-BR CPF in overwrite mode
- **THEN** it holds `923,856,789-01` with the caret at 5

#### Scenario: Insert on a full field

- **WHEN** `9` is typed at 0 of a full mask `00000-000` in insert mode
- **THEN** nothing changes

### Requirement: A mask remembers what it showed

A `MaskFormatter` SHALL keep a history of the values it answered.

- A run of typing is one step, until a deletion, a paste, a caret moved elsewhere or a pause.
- A new step drops what could be redone.
- The history holds at most 100 steps.
- A `current` value it did not answer clears the history.

`TextCommand.Undo` and `Redo` SHALL answer the text and the selection of the step, and null when there is none.

#### Scenario: Undo a paste

- **WHEN** `123` is typed into a pt-BR CPF, `45678901` pasted after it, and `Undo` handed twice
- **THEN** the first answers `123` with the caret at 3, and the second an empty text

#### Scenario: A value from outside is not undone

- **WHEN** `123` is typed, the next edit is handed a `current` of `""`, and `Undo` is handed
- **THEN** the answer is null

### Requirement: A mask reads the values its text holds

`MaskFormatter.Read` SHALL answer a `MaskedText` holding:

- `Value`, with no literals and no prompts;
- `Text`, with the literals;
- `MaskCompleted` and `MaskFull`.

`Fit` SHALL place a value as `MaskedTextBox.Text` is set, so the raw value and the formatted text both fill it.
`IsLiteral` SHALL say which characters of a text are the mask's literals.

The text a `MaskFormatter` writes SHALL hold the literals and no prompts, cut after the last assigned position,
with a space for an unassigned position before it.

#### Scenario: Raw in, both out

- **WHEN** `Fit("12345678901")` is read by a pt-BR CPF formatter, and its last digit is then deleted
- **THEN** the text is `123,456,789-0`, its `Value` is `1234567890`, and `MaskCompleted` is false

#### Scenario: An optional gap survives the round trip

- **WHEN** the `Value` of `(1_) 2345-678_` in mask `(99) 9999-9999` is fitted again
- **THEN** the text is the same, the gap included

### Requirement: The mask engine is an assembly the SDK delivers

The mask engine and its public surface (`MaskFormatter`, `MaskedText`, `InsertKeyMode`) SHALL ship as the assembly
and package `eQuantic.UI.Masking`, of surface class `contract`, whose types the runtime provides. Both SDKs SHALL
reference it, by package and, beside a source tree, by project, and SHALL put `eQuantic.UI.Masking` in scope, so
an app's csproj names nothing.

#### Scenario: An app that writes only its SDK

- **WHEN** an app whose csproj is `<Project Sdk="eQuantic.UI.Sdk">` and nothing else builds a page that holds a
  `MaskedText` and a `MaskFormatter`
- **THEN** it builds with no `using` and no `PackageReference` of its own, and its module imports them from
  `@equantic/runtime`

#### Scenario: The surface is declared

- **WHEN** a public member is added to `eQuantic.UI.Masking` without a line in its `PublicAPI.Unshipped.txt`
- **THEN** the build fails with RS0016
