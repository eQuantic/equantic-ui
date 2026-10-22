## Why

Closes #1010, the first part of slice 2 of docs/CODE-EDITOR-PLAN.md (#601). The editor's keys are
decided by hand in one function, `CodeKeymap.Handle`, so an app cannot bind an editor action to a key of
its own, cannot run one from a menu or a palette, and cannot free a key the editor takes. The plan
decided the shape in its second section, "Keys are commands, and the keymap is data", and the rest of
slice 2 (line operations #1011, multiple cursors #1012, snippets #1013) lands as entries of that table.

Written by hand, the keymap also reads its modifiers loosely: measured over every key and every
combination of the four modifiers, it claims ⌃⌥⇧← on a Mac as a step left, ⌘⌥A as select all and
⇧Tab as accepting a completion, none of which any IDE binds. A table names its chords exactly.

## What Changes

For a developer using the SDK:

- Every action the editor's keys run is a command, `CodeCommand` (the caret's moves with and without
  the selection, the edits, the clipboard, undo and redo, the completion list, the signature panel, the
  card and the problems). `CodeEditorController.Execute(command)` runs one with no key, as a menu or a
  palette does, and answers whether it did anything. `CanExecute(command)` says whether it would.
- The keymap is a table of `CodeKeyBinding`s, a `KeyChord` (the SDK's, as `Shortcut` takes it) to a
  command, with the chord that comes first for a binding of two keys (⌘K ⌘I). `CodeKeymap.Defaults(
  convention)` is the table for the Apple and the Standard keyboard, and one chord may name several
  commands in order: the first that runs claims the key (↓ walks a list showing, then the signature
  panel's overloads, then moves the caret).
- `CodeEditor.KeyBindings` (and the controller's `KeyBindings`) puts an app's bindings over the
  defaults: a chord the app binds is the app's alone, and binding a chord to `CodeCommand.None` gives it
  back to the host.
- A chord the table does not name is not the editor's, so the combinations the hand-written keymap
  claimed by accident go to the host: the arrows, Home, End and the page keys with a modifier no
  convention gives them, Enter, Backspace, Delete, Tab and Escape with a modifier no command uses (⌘Enter,
  ⇧Delete, ⌘Escape among them), the space with ⌘ and ⌃ together, and the letter chords with an extra
  modifier (⌘⌥A, ⇧⌘C). Measured against the hand-written keymap, three
  named chords change, all on purpose: ⇧Tab and ⇧Enter over a completion list outdent and break the line
  instead of accepting the entry, as VS Code has them, and ⌥↑ ⌥↓ walk the signature panel's overloads
  and otherwise no longer move the caret, which line operations (#1011) take them for.

The parts it reaches: the engine (`eQuantic.UI.Code`: the commands, the bindings, the table and its
dispatch), the component (`CodeEditor.KeyBindings`), and through the twins both realizers, which call
the same function as before. Nothing new in the vocabulary, eqc or the runtime's own code.

A break, on purpose and in preview: `CodeKeymap` keeps `Handle` and gains `Defaults`, and the loose
combinations above stop being the editor's.

## Capabilities

### New Capabilities

- `code-keymap`: the editor's commands, the default tables for both keyboard conventions, the order in
  which one chord's commands are tried, two-key chords, and an app's own bindings.

### Modified Capabilities

None: the keys every spec names keep their meaning.
