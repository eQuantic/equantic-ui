## ADDED Requirements

### Requirement: A formatter decides every edit of a text entry

A web `TextEntry` with a `Formatter` SHALL hand every edit to the formatter before it lands, as a `TextEdit` with
the entry's `TextEditingValue`, and SHALL show the text and the selection the formatter answers, handing the text
to `OnChanged` when it changed. An edit the formatter refuses SHALL change nothing.

#### Scenario: A web edit, caret included

- **WHEN** `9` is typed at 2 of a formatted web field holding `123.456.78`, in Chrome and in Firefox
- **THEN** the field shows the formatter's text with the caret where the formatter put it, not at the end

#### Scenario: A Backspace the formatter owns

- **WHEN** Backspace is pressed in a formatted web field
- **THEN** the formatter receives a `DeletedBackward` edit over the position before the caret, and the browser
  deletes nothing of its own

### Requirement: A composition is never formatted while it is composed

A web `TextEntry` with a `Formatter` SHALL let a composition (an input method's, a dead key's) show as the browser
shows it, unformatted. When the composition is committed, the field SHALL go back to the text and selection it had
when the composition started, and hand the committed text to the formatter as one typed edit. A cancelled
composition SHALL leave the field as it was.

#### Scenario: Fullwidth digits from an input method

- **WHEN** an input method composes `１２` in a formatted web field of mask `00000-000` and commits it
- **THEN** the field shows `１２` while composing, and the formatter's answer for typing `１２` after the commit

### Requirement: A formatted entry is not prose

A web `TextEntry` whose formatter says it is not prose SHALL be served without spelling, autocorrection or
automatic capitals, whatever its kind.

#### Scenario: A plate on the web

- **WHEN** a formatted web field of kind `Text` that is not prose is served
- **THEN** its input carries `spellcheck="false"`, `autocapitalize="none"` and `autocorrect="off"`

### Requirement: The targets' undo, redo and Insert reach a formatter

A web `TextEntry` with a `Formatter` SHALL hand it `TextCommand.Undo`, `Redo` and `ToggleOverwrite` from every way
the browser offers them, and SHALL show the state the formatter answers. A command the formatter does not take
SHALL do what the browser does without one.

- The keys: `Ctrl`/`Cmd+Z`, `Ctrl`/`Cmd+Shift+Z`, `Ctrl+Y` and `Insert`.
- The `historyUndo` and `historyRedo` the browser's menus and gestures dispatch, which the field keeps armed by
  writing its text with `execCommand('insertText')`.

#### Scenario: The browser's own undo, cancelled

- **WHEN** `Meta+Z` is pressed in a formatted web field after two typed characters, in Chrome and in Firefox
- **THEN** the field receives a cancelable `historyUndo`, cancels it, and shows the formatter's answer

### Requirement: An obscured formatted entry shows the formatter's literals

An obscured web `TextEntry` with a `Formatter` SHALL be a real password input holding the whole text, with the
characters the formatter calls literals drawn as they are over their bullets, hidden from assistive technology,
and SHALL copy nothing out of it.

#### Scenario: A literal over the bullets

- **WHEN** an obscured web entry whose formatter calls position 4 a literal holds `1234-5`, and its input scrolls
- **THEN** its input is of type `password`, and the drawn `-` stays over the fifth bullet as the input scrolls
