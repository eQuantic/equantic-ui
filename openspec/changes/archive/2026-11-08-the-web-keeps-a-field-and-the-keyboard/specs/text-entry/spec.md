## ADDED Requirements

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
