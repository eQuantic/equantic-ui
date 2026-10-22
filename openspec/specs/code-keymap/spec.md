# code-keymap Specification

## Purpose
What a key means to a code editor, on both targets: every action a key runs is a command an app can run with no key, the keymap is a table of chords to commands for each keyboard convention, and an app binds its own chords over the defaults.

## Requirements

### Requirement: Keys run commands

Every key an editor claims SHALL run a `CodeCommand`. `CodeEditorController.Execute` SHALL run a command
with no key, as a menu or a palette does, and answer whether it did anything; `CanExecute` SHALL answer
whether it would.

#### Scenario: A command run from code

- **WHEN** an app runs `Execute(CodeCommand.SelectAll)` on an editor holding three lines
- **THEN** the whole document is selected, and `Execute` answers true

#### Scenario: A command with nothing to do

- **WHEN** an app runs `Execute(CodeCommand.CompletionNext)` while no completion list shows
- **THEN** nothing changes, and `Execute` answers false

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

### Requirement: A chord of two keys

A binding MAY name the chord that comes first. That first chord SHALL be claimed only while the second
half's command could run, and it SHALL wait for one more key: any key but a modifier ends the wait, and
runs the command when it is the second half.

#### Scenario: ⌘K ⌘I

- **WHEN** a hover provider is set, ⌘K is pressed and then ⌘I
- **THEN** the card for the caret shows

#### Scenario: Nothing to explain

- **WHEN** no hover provider is set and no problem is reported, and ⌘K is pressed
- **THEN** the editor does not claim the key

#### Scenario: A key between the halves

- **WHEN** ⌘K is pressed, then ⌃Space opens the completion list and Escape closes it, then ⌘I is pressed
- **THEN** no card shows

### Requirement: An app's bindings come first

The bindings an app gives an editor (`CodeEditorController.KeyBindings`, `CodeEditor.KeyBindings`) SHALL
be tried before the defaults, and a chord the app binds SHALL NOT be looked up in the defaults: binding a
chord to `CodeCommand.None` gives it back to the host.

#### Scenario: A key of the app's own

- **WHEN** an app binds ⌘D to `CodeCommand.SelectAll` and ⌘D is pressed
- **THEN** the whole document is selected

#### Scenario: A key given back

- **WHEN** an app binds ⌘A to `CodeCommand.None` and ⌘A is pressed
- **THEN** the editor does not claim the key, and the selection does not change
