## ADDED Requirements

### Requirement: A formatter decides every edit of a text entry

A `TextEntry` with a `Formatter` SHALL hand every edit, on the web and on Photon, to the formatter before it
lands, as a `TextEdit` (the range it replaces, the text, and whether it was typed, pasted, or deleted backward
or forward) with the entry's `TextEditingValue`, and SHALL show the text and the selection the formatter
answers, handing the text to `OnChanged` when it changed. An edit the formatter refuses SHALL change nothing.
`TextInput` and `FormInput` SHALL take a `formatter` and hand it to their entry.

#### Scenario: A web edit, caret included

- **WHEN** `9` is typed at 2 of a formatted web field holding `123.456.78`, in Chrome and in Firefox
- **THEN** the field shows the formatter's text with the caret where the formatter put it, not at the end

#### Scenario: A Backspace the formatter owns

- **WHEN** Backspace is pressed in a formatted field, on the web and on Photon
- **THEN** the formatter receives a `DeletedBackward` edit over the position before the caret, and the browser
  or the host deletes nothing of its own

#### Scenario: Undo

- **WHEN** `Meta+Z` or `Ctrl+Z` is pressed in a formatted web field
- **THEN** nothing changes

### Requirement: A composition is never formatted while it is composed

A `TextEntry` with a `Formatter` SHALL let a composition (an input method's, a dead key's) show as the
target shows it, unformatted, and when it is committed SHALL go back to the text and selection it had when the
composition started and hand the committed text to the formatter as one typed edit. A cancelled composition
SHALL leave the field as it was.

#### Scenario: Fullwidth digits from an input method

- **WHEN** an input method composes `１２` in a formatted web field of mask `00000-000` and commits it
- **THEN** the field shows `１２` while composing, and the formatter's answer for typing `１２` after the commit

#### Scenario: A dead key on Photon

- **WHEN** `´` then `e` are typed into a formatted Photon field of mask `LLLL` on macOS
- **THEN** the composition shows marked, and `é` is handed to the formatter once it commits

### Requirement: A phone's whole new value reaches a formatter as one edit

On Photon, a phone keyboard's whole new `TextEditingValue` SHALL be turned into one `TextEdit` against the
field's current value before the formatter sees it: the range and the text from the common prefix and
suffix, a deletion's direction from where the caret was and where it went, one inserted character as typed and
more as pasted; a value with a composing range SHALL be kept as sent until the range clears.

#### Scenario: A digit from Android's number keyboard

- **WHEN** the keyboard sends `1234` with the caret at 4 to a pt-BR CPF field holding `123`
- **THEN** the formatter receives `Typed` `4` at 3, and the keyboard is handed back `123,4` with the caret at 5

### Requirement: A formatted entry is not prose

A `TextEntry` whose formatter says it is not prose SHALL be edited without spelling, autocorrection,
suggestions or automatic capitals on every target, whatever its kind.

#### Scenario: A plate on the web

- **WHEN** a formatted web field of kind `Text` that is not prose is served
- **THEN** its input carries `spellcheck="false"`, `autocapitalize="none"` and `autocorrect="off"`
