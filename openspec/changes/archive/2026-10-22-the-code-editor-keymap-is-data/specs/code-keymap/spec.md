# Spec Delta

## ADDED Requirements

### Requirement: Keys run commands

Every key an editor claims SHALL run a `CodeCommand`. `CodeEditorController.Execute` SHALL run a command
with no key, as a menu or a palette does, and answer whether it did anything; `CanExecute` SHALL answer
whether it would, so a command it answers true for does something when it runs.

#### Scenario: A command run from code

- **WHEN** an app runs `Execute(CodeCommand.SelectAll)` on an editor holding three lines
- **THEN** the whole document is selected, and `Execute` answers true

#### Scenario: A command with nothing to do

- **WHEN** an app runs `Execute(CodeCommand.CompletionNext)` while no completion list shows
- **THEN** nothing changes, and `Execute` answers false

#### Scenario: A list of one entry

- **WHEN** a completion list shows one entry
- **THEN** `CanExecute` answers false for walking it (`CompletionNext`, `CompletionPrevious` and the page
  commands) and true for accepting it and hiding it

### Requirement: The defaults name their chords exactly

The default bindings SHALL be one table for each keyboard convention, `CodeKeymap.Defaults`. A key SHALL
match a binding only with exactly the binding's modifiers, a single letter in either case, and a chord no
binding names SHALL NOT be claimed by the editor.

#### Scenario: A word to the left on a Mac

- **WHEN** ⌥← is pressed in an editor using the Apple convention
- **THEN** the caret steps to the start of the word on its left

#### Scenario: A combination no convention names

- **WHEN** ⌃⌥⇧← is pressed in an editor using the Apple convention
- **THEN** the editor does not claim the key, and the caret does not move

### Requirement: One chord tries its commands in order

A chord MAY name several commands, and the editor SHALL run them in the table's order until one runs:
that one claims the key, and when none runs the key is not claimed.

#### Scenario: The down arrow

- **WHEN** ↓ is pressed while a completion list shows, then while the signature panel shows three
  overloads and no list, then with neither
- **THEN** it selects the list's next entry, then shows the next overload, then moves the caret down a line

#### Scenario: A command that finds nothing to do

- **WHEN** an app binds ⌘J to `CodeCommand.HoverShow` and then to `CodeCommand.SelectAll`, no hover
  provider is set and no problem is at the caret, and ⌘J is pressed
- **THEN** no card shows, and the whole document is selected

### Requirement: A chord of two keys

A binding MAY name the chord that comes first. That first chord SHALL be claimed only while the second
half's command could run, and it SHALL wait for one more key: any key but a modifier ends the wait, and
runs the command when it is the second half. The key that completes the chord SHALL be claimed even when
its command finds nothing to do, as VS Code claims it.

#### Scenario: ⌘K ⌘I

- **WHEN** a hover provider is set, ⌘K is pressed and then ⌘I
- **THEN** the card for the caret shows

#### Scenario: Nothing to explain

- **WHEN** no hover provider is set and no problem is at the caret, one being reported on another line, and
  ⌘K is pressed
- **THEN** the editor does not claim the key

#### Scenario: The key that completes the chord

- **WHEN** ⌘K is pressed with the caret on a problem, the problem is withdrawn, and ⌘I is pressed
- **THEN** the editor claims ⌘I, and no card shows

#### Scenario: A key between the halves

- **WHEN** ⌘K is pressed, then ⌃Space opens the completion list and Escape closes it, then ⌘I is pressed
- **THEN** no card shows

### Requirement: An app's bindings come first

The bindings an app gives an editor (`CodeEditorController.KeyBindings`, `CodeEditor.KeyBindings`) SHALL
be tried before the defaults, single keys and the first halves of chords of two keys alike, and a chord
the app binds SHALL NOT be looked up in the defaults: binding a chord to `CodeCommand.None` gives it back
to the host.

#### Scenario: A key of the app's own

- **WHEN** an app binds ⌘D to `CodeCommand.SelectAll` and ⌘D is pressed
- **THEN** the whole document is selected

#### Scenario: A key given back

- **WHEN** an app binds ⌘A to `CodeCommand.None` and ⌘A is pressed
- **THEN** the editor does not claim the key, and the selection does not change

#### Scenario: A chord of two keys of the app's own over a default

- **WHEN** an app binds ⌘A then ⌘D to `CodeCommand.CursorDocumentEnd`, and ⌘A then ⌘D are pressed
- **THEN** ⌘A selects nothing, and ⌘D puts the caret at the end of the document

### Requirement: The clipboard's keys on the web

On the web, a key the browser answers with a clipboard event of its own (C, X and V with ⌘ or Ctrl, and
off a Mac Ctrl+Insert, Shift+Insert and Shift+Delete) SHALL be left to that event, which carries the text.
Any other key an app binds to `CodeCommand.Copy` or `CodeCommand.Cut` SHALL reach the clipboard through the
browser's Clipboard API. A key an app binds to `CodeCommand.Paste` finds nothing to read there, since a page
reads the clipboard only in a paste event or behind a prompt.

#### Scenario: Copy on a key of the app's own

- **WHEN** an app binds ⌘D to `CodeCommand.Copy` on the web, selects the whole document, and presses ⌘D,
  then ⌘C
- **THEN** ⌘D writes the text through the Clipboard API, and ⌘C is not claimed, so the browser's copy event
  carries the text
