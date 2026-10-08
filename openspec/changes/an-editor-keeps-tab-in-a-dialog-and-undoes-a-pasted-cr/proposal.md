## Why

Closes #598 and #600, both sub-issues of #419 (the code editor: an IDE-grade, write-once editor).

- #598: a `CodeEditor` keeps Tab for itself (Tab indents, Shift+Tab outdents) until Escape releases
  it. Inside a dialog on the web, the dialog's focus trap heard Tab in the capture phase, before the
  editor's input: with the editor last in the dialog, Tab moved the focus to the first control and the
  line was never indented, and Shift+Tab did the same with the editor first. Photon was right: its
  host asks the code target before it cycles the focus.
- #600: the document breaks lines on CR, CRLF and LF alike, and an edit recorded the text as it was
  handed, which the history split on LF alone. Pasting "x\ry" into "abc" after its first character
  gave "ax" and "ybc", and undo left "a" and "ybc".

## What Changes

For a developer, with no line of their code changed:

- A code editor at either end of a dialog indents on Tab and outdents on Shift+Tab, on the web as on
  Photon. After Escape, the next Tab moves on as the dialog cycles.
- Undo after any paste restores the document exactly, and redo replays it, whatever breaks the
  pasted lines.
- `CodeEditorController.Changed` carries the text the document holds: a paste that held CR or CRLF
  arrives with LF, so an IDE's language server and diff see the lines the editor shows.

The parts it reaches: the code engine (`eQuantic.UI.Code`: `CodeEdit`, `CodeEditorController`) and
its runtime twins, and the runtime's focus trap (`dom/focus-trap.ts`). eqc, the web realizer and
the Photon shells do not move. Neither the public surface nor the developer surface moves.

## Capabilities

### New Capabilities

- `code-history`: what an edit records and what undo and redo restore.
- `modal-focus`: how a modal layer's focus trap shares Tab with the control that has the focus.

### Modified Capabilities

None.

## Impact

The engine and the runtime only. A control that consumes Tab inside a dialog (a code editor, a
spreadsheet walking its cells) now keeps it on the web as it does on Photon.
