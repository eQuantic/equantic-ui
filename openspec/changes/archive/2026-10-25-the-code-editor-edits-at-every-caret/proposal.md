## Why

Closes #1012, the third part of slice 2 of docs/CODE-EDITOR-PLAN.md (#601), on the commands of #1027, the
line operations of #1033 and the undo step of several edits of #1031. The parity bar's row "Multiple
cursors, add next occurrence, column selection" is open on both targets: an editor held one selection,
so renaming a word in five places was five edits, and a column of values could not be typed into at once.

## What Changes

For a developer using the SDK, and for the person typing in the editor it gives them:

- `CodeEditorController.Selections` holds every selection, the primary first and the others in document
  order, made one where they meet as VS Code merges its cursors. The primary is the selection placed last,
  as Rider's primary caret is: the one brought into view, the one an input method and the completion list
  stand at, and the one Escape keeps. `Selection` stays the primary, and setting it leaves one selection.
- ⌥⌘↑ and ⌥⌘↓ (Ctrl+Alt+↑, Ctrl+Alt+↓) add a caret above or below every caret, at the cell it aims at.
  ⌥ and a press adds a caret, or takes away the one pressed on; ⇧⌥ and a press or a drag selects a column,
  one selection per line the box reaches, as VS Code's column selection does.
- ⌘D (Ctrl+D) selects the word at the caret, then adds the next occurrence of the selection, wrapping at
  the document's end; ⇧⌘L (Ctrl+Shift+L) selects every occurrence. Escape goes back to one caret, after
  the list, the card and the panel, and before the trap on Tab.
- Typing, deleting, Enter, Tab and ⇧Tab, ⌘/, the line operations, ⌘↩, a cut, a paste and a completion
  accepted act at every caret, each as ONE undo step heard as one change, and undo and redo put back every
  caret. A paste with a line for each caret gives each caret its line; a copy takes every selection, a line
  each. Tab, ⇧Tab and ⌘/ over lines are an edit a line now, so a problem on an indented or commented line
  travels with its text, and two carets on one line indent it once.
- Every caret is drawn, every selection's bands and every caret's line, on the web and on Photon, from the
  same C#. Moving the carets moves each one, each keeping the cell it aims at.
- And #1063, which the code block's twin met: eqc left an empty list of tuples unannotated in TypeScript,
  so its pushes made it an array of arrays where a list of pairs was returned, and `tsc` refused it. eqc
  annotates it with the tuple of its elements' names now, as it writes a tuple a method returns.

The parts it reaches: eqc (`LocalDeclarationStrategy`), the engine (`eQuantic.UI.Code`: the controller's carets, every command at every
caret, the history's `CodeStep`, the keymap's new chords and the commands appended to `CodeCommand`), the
`CodeBlock` and `CodeDiff` components, and through the twins both realizers, which already paint every
caret the model lists. The public surface moves: `CodeHistory.Record` takes a `CodeStep` and `Undo` and
`Redo` answer every selection, where both held one; `CodeBlock.ActiveLine` and `Selection` become
`ActiveLines` and `Selections`. The migration lines are "an app that drove `CodeHistory` itself records a
`CodeStep` and reads the selections `Undo` answers" and "a code block given one active line or one
selection is given a list of them".

## Capabilities

### New Capabilities

- `code-cursors`: several selections, the primary, adding and removing carets by key, by pointer and by
  occurrence, column selection, every command at every caret, the clipboard and the completion at every
  caret, and every caret drawn.

### Modified Capabilities

- `code-history`: a step restores every selection, and typing at several carets is one run.
