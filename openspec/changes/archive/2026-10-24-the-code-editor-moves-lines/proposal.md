## Why

Closes #1011, the second part of slice 2 of docs/CODE-EDITOR-PLAN.md (#601), on the keymap #1027 made
data. The parity bar's row "Line operations (move, copy, delete, join, insert above/below)" is open on
both targets: an editor cannot move a line, copy it, delete it, join two, sort a block or open a line
above or below the caret, which every IDE a person comes from does with one key.

## What Changes

For a developer using the SDK, and for the person typing in the editor it gives them:

- ⌥↑ ⌥↓ move the lines the selection touches one line up or down, the selection moving with them.
  While the signature panel shows overloads it keeps the two keys, as it does today, and the lines move
  otherwise.
- ⇧⌥↑ ⇧⌥↓ copy the lines above or below themselves.
- ⇧⌘K (Ctrl+Shift+K) deletes the lines, their line breaks included.
- ⌃J on a Mac joins the lines, or the caret's line with the next, as VS Code joins them.
- Sorting the lines, ascending or descending, is a command with no default key, as in VS Code.
- ⌘↩ (Ctrl+Enter) opens a line below the caret's, and ⇧⌘↩ (Ctrl+Shift+Enter) one above it, indented
  as Enter would indent it.

Each is a `CodeCommand` (`MoveLinesUp`, `MoveLinesDown`, `CopyLinesUp`, `CopyLinesDown`, `DeleteLines`,
`JoinLines`, `SortLinesAscending`, `SortLinesDescending`, `InsertLineAbove`, `InsertLineBelow`), a method
of `CodeEditorController` an app can call, and ONE undo step. Undo puts back the selection the operation
found, and redo the one it left, where today an edit's redo comes back to the caret alone.

The parts it reaches: the engine (`eQuantic.UI.Code`: the commands, the controller's methods, the
default tables), and through the twins both realizers. Nothing new in the vocabulary, eqc or the
runtime's own code.

## Capabilities

### New Capabilities

- `code-lines`: the editor's line operations, the lines each takes, the selection each leaves, and the
  keys that run them in both conventions.

### Modified Capabilities

- `code-signature-help`: with one signature, or under a card, ↑ and ↓ move the caret and ⌥↑ ⌥↓ move the
  lines.

## Impact

- `CodeCommand` gains ten members, appended: the enum is append-only, its values pinned.
- The default tables gain ⌥↑ ⌥↓ after the panel's, ⇧⌥↑ ⇧⌥↓, ⇧⌘K, ⌃J on a Mac, ⌘↩ and ⇧⌘↩. A chord no
  table named before is the editor's now, so a host that read ⌘↩ or ⇧⌘K in an editor stops receiving it,
  which the keymap's behaviour baseline names line by line.
