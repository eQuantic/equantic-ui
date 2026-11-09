## ADDED Requirements

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
