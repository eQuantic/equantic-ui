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

#### Scenario: A language with no line comment

- **WHEN** an editor holds JSON, which has no line comment
- **THEN** `CanExecute(CodeCommand.ToggleLineComment)` answers false, and ⌘/ is not claimed

#### Scenario: A deletion with nothing to take

- **WHEN** the caret stands at the start of the document with nothing selected
- **THEN** `CanExecute` answers false for `DeleteLeft`, `DeleteWordLeft` and `DeleteToLineStart`, and true
  for `DeleteRight`

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
to the host. A key the app binds on its own SHALL take with it the default chords of two keys that start on
it. A `CodeEditor`'s bindings SHALL go to the controller after the controller's own, and the ones a later
build stops handing SHALL come out whole, leaving the controller's own in their order.

#### Scenario: A key of the app's own

- **WHEN** an app binds ⌘D to `CodeCommand.SelectAll` and ⌘D is pressed
- **THEN** the whole document is selected

#### Scenario: A key given back

- **WHEN** an app binds ⌘A to `CodeCommand.None` and ⌘A is pressed
- **THEN** the editor does not claim the key, and the selection does not change

#### Scenario: A chord of two keys of the app's own over a default

- **WHEN** an app binds ⌘A then ⌘D to `CodeCommand.CursorDocumentEnd`, and ⌘A then ⌘D are pressed
- **THEN** ⌘A selects nothing, and ⌘D puts the caret at the end of the document

#### Scenario: A first half given back

- **WHEN** a hover provider is set, an app binds ⌘K to `CodeCommand.None`, and ⌘K then ⌘I are pressed
- **THEN** the editor claims neither key, and no card shows

#### Scenario: A component's bindings withdrawn

- **WHEN** a controller holds ⌘D bound to select all and then to the document's end, its `CodeEditor` hands
  ⌘D bound to select all as well, and the next build hands none
- **THEN** the controller holds its two bindings in their order, and ⌘D selects all

### Requirement: The clipboard on the web

On the web, `CodeCommand.Copy` and `CodeCommand.Cut` SHALL write through the browser's own copy event, which
the key's handler fires (`document.execCommand('copy')`), on whatever key they are bound to, as VS Code's
do, and the command bound to the key SHALL decide, never the key. A cut SHALL delete only what that event
took to the clipboard, and a write the browser refused SHALL leave the key to the browser. A paste SHALL be
the browser's paste event: on a key the browser pastes on, the key goes on to the browser, whose event
brings the text, and a key an app binds to `CodeCommand.Paste` elsewhere finds nothing to read, since a
page reads the clipboard only in a paste event.

#### Scenario: Copy on a key of the app's own

- **WHEN** an app binds ⌘D to `CodeCommand.Copy` on the web, selects the whole document, and presses ⌘D
- **THEN** the browser's copy event takes the text to the clipboard, and the editor claims the key

#### Scenario: Copy on the browser's cut key

- **WHEN** an app binds ⌘X to `CodeCommand.Copy` on the web and presses it over a selection
- **THEN** the text is copied, and nothing is deleted

#### Scenario: A cut the browser takes no copy for

- **WHEN** a key bound to `CodeCommand.Cut` is pressed on the web and the browser fires no copy event
- **THEN** nothing is deleted, and the editor does not claim the key

#### Scenario: Paste on ⌘V

- **WHEN** ⌘V is pressed on the web
- **THEN** the editor does not claim the key, and the browser's paste event pastes the clipboard's text
