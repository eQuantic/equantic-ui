# text-entry Specification

## Purpose
How a text entry behaves as it takes the keyboard: an autofocused entry puts its caret at the end of the text it holds, on the web as on Photon.

## Requirements

### Requirement: An autofocused entry puts its caret at the end

A `TextEntry` with `Autofocus` SHALL take the keyboard when it is mounted with its caret at the end of the
text it holds, on the web as on Photon, as Flutter's autofocused `TextField` does.

#### Scenario: A field that opens holding a prefix

- **WHEN** an autofocused entry holding `:` is mounted and `12` typed
- **THEN** it holds `:12`

### Requirement: A field with a page behind it shows what the page holds

On the web, after a render, a `TextEntry` with an `onChanged` SHALL show the value the page holds,
whatever the person typed and whether or not the value the page holds changed since the last render, on
a single-line field and on a multiline one. A `TextEntry` with no `onChanged` SHALL keep what the person
typed.

#### Scenario: A reset in the same render as the typing

- **WHEN** a field's text is typed and the page resets its value to `""` before the next frame
- **THEN** the field shows nothing after the render

#### Scenario: A multiline reset

- **WHEN** a field of three lines is typed in, a frame passes, and the page resets its value to `""`
- **THEN** the field shows nothing after the render

#### Scenario: A field with no page behind it

- **WHEN** a field with no `onChanged` is typed in and its page renders again
- **THEN** the field keeps what was typed

### Requirement: A Photon field keeps what was typed until the page builds it

On Photon, a `TextEntry` SHALL show what was typed into it until the page builds it again, and then
SHALL show the value the page hands it, whether or not that value changed since the page last built
it. Its measure, its paint, what it tells assistive technology, its caret and selection, and the
editing value a phone's keyboard reads SHALL all read that one text. A field drawn at another field's
place SHALL start from its own value.

#### Scenario: A handler that ignores the key

- **WHEN** a field's handler neither keeps the key nor calls `SetState`, and `a` is typed into it
- **THEN** the field shows `a`, and no component builds in the frame after the key

#### Scenario: A handler that keeps the value without saying so

- **WHEN** a form keeps its field's value without `SetState`, `a` and then `b` are typed, and the form
  later builds for another reason
- **THEN** the field shows `ab` after the keys and after the form builds

#### Scenario: A field component's own rebuild

- **WHEN** a form keeps its field's value without `SetState` through a `TextInput`, `ab` is typed, and the
  focus leaves the field, so the `TextInput` builds again on its own
- **THEN** the field shows `ab`

#### Scenario: A handler that rejects the key

- **WHEN** a field holding `ab` is typed `!` into, and its handler calls `SetState` keeping `ab`
- **THEN** the field shows `ab` after the frame

#### Scenario: A formatter

- **WHEN** `abc` is typed into a field whose handler calls `SetState` with the text upper-cased
- **THEN** the field shows `ABC`

#### Scenario: A reset in the same frame as the typing

- **WHEN** `123` is typed into a field whose page held `""`, and before the next frame the page calls
  `SetState` to hold `""` again
- **THEN** the field shows nothing after the frame

#### Scenario: Everything that reads the field reads what was typed

- **WHEN** `ab` is typed into a field whose handler calls no `SetState`
- **THEN** the frame draws `ab`, assistive technology reads `ab`, the caret sits after `b`, and a
  phone's keyboard reads `ab` as the field's editing value

#### Scenario: Another field at the same place

- **WHEN** a field holding typed text leaves the tree and another field is drawn at its place
- **THEN** the new field shows its own value

### Requirement: An entry may select its text as it takes the keyboard

A `TextEntry` with `SelectOnFocus` SHALL select all of its text whenever it takes the keyboard, autofocused or
not, on the web as on Photon, so that what is typed first replaces it.

#### Scenario: A rename field

- **WHEN** an autofocused entry with `SelectOnFocus` holding `App.cs` is mounted and `Main.cs` typed
- **THEN** it holds `Main.cs`

### Requirement: A text entry says what kind of value it takes

A `TextEntry` SHALL take a `Kind`, a `TextEntryKind` of `Text` (the default), `Email`, `Phone`, `Url`,
`Number` or `Decimal`. On the web, a single-line entry SHALL lower to the input that opens each kind's
keyboard and offers its autofill: `Email` to `type="text"`, `inputmode="email"` and
`autocomplete="email"`; `Phone` to `type="tel"`, `inputmode="tel"` and `autocomplete="tel"`; `Url` to `type="text"`,
`inputmode="url"` and `autocomplete="url"`; `Number` to `type="text"` and `inputmode="numeric"`;
`Decimal` to `type="text"` and `inputmode="decimal"`. Every kind but `Text` SHALL say
`spellcheck="false"` and `autocapitalize="none"`. An obscured entry SHALL stay `type="password"`, with
its kind's `inputmode` and no `autocomplete`. The server's lowering and the browser's SHALL be the same element. A
multi-line entry SHALL take no kind's attributes.

#### Scenario: An e-mail field

- **WHEN** an entry of kind `Email` is lowered on the server and in the browser
- **THEN** both are an `input` with `type="text"`, `inputmode="email"`, `autocomplete="email"`,
  `spellcheck="false"` and `autocapitalize="none"`

#### Scenario: A PIN

- **WHEN** an obscured entry of kind `Number` is lowered
- **THEN** it is `type="password"` with `inputmode="numeric"`

#### Scenario: The caret of an autofocused e-mail field

- **WHEN** an autofocused entry of kind `Email` holding `a@b` is mounted in a browser
- **THEN** its caret is at the end of `a@b` and nothing throws

### Requirement: A field may leave its label out of the drawing

A `TextInput`, a `DatePicker` or a `DateTimePicker` (its date's field) with `LabelHidden` SHALL draw no
label above its field, and the field SHALL still be named by the label for assistive technology. A page
that draws it again with `LabelHidden` changed SHALL see the change.

#### Scenario: A form that draws its own label

- **WHEN** a form draws `Text("Delivery day")` above a `DatePicker` labelled "Delivery day" with
  `LabelHidden`
- **THEN** one static text named "Delivery day" is drawn, and the field is named "Delivery day"

#### Scenario: Hidden on a redraw

- **WHEN** the form starts drawing its own label and hands the picker `LabelHidden` on the next drawing
- **THEN** the picker's label leaves the drawing
