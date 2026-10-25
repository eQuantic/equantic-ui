## Why

Closes #1031, a bug under #601, and #1034, a bug under #849 (slice 2 of docs/CODE-EDITOR-PLAN.md) that #1033's review found. A line
operation is one replacement over every line it rewrites, so `CodeEdit.Carry` takes a problem inside that
span to the replacement's ends: a problem on a line ⌥↑ moves stretches over the moved line and the one it
passed, its wave and its gutter mark with it, until a provider answers again, and a problem the app
reported itself is never answered again.

The history records ONE replacement per step, which is what forces a move to rewrite both lines. The
multiple cursors of #1012 need the same thing this does, an undo step that holds several replacements, so
the mechanism is built here, where one command proves it.

## What Changes

For a developer using the SDK, and for the person typing in the editor it gives them:

- ⌥↑ and ⌥↓ move the line they pass to the block's other side, as two replacements in one undo step: the
  passed line is deleted and written again beyond the block, so a problem on a moved line stays on its
  text and never stretches. A problem on the passed line is left empty where its text was, as a deleted
  text's problem is, until a provider answers again: of the two sides one is written anew, and it is the
  side the selection is not on.
- An undo step is the edits a command made, in the order it made them. Undo takes them back last first,
  redo makes them again first first, and the editor raises `Changed` once for the step either way, with
  every replacement in the order made, so a listener that carries ranges through edits (the problems, a
  language server) sees what happened once it has happened, as a language server's `didChange` does.
- `CodeHistory.Record` takes the edits of one step, and `CodeHistory.Undo` and `Redo` answer the
  replacements they made, where they answered one range replaced and one written. A typed character still
  joins the run of typing before it, a step of one edit, as today.

- And #1034, which the history's twin met: a method with outs declares them in front of the arrow its
  body runs in, untyped even in TypeScript, so an out assigned an empty list read as an implicit `any[]`
  and `tsc` refused the twin. eqc declares them `any` in TypeScript now, as every variable an arrow binds
  is, and nothing in the plain JavaScript the conformance harness runs.

The parts it reaches: the engine (`eQuantic.UI.Code`: `CodeHistory`, the controller's undo and redo, the
move), eqc (`OutParameters`), and through the twins both realizers. The public surface moves: `CodeHistory.Undo` and `Redo`
change their shape, `CodeHistory.Record` takes a list, and `CodeEditorController.Changed` carries a
`CodeChange`, the edits of one change, where it carried one `CodeEdit` or null. No app in this repository
calls them; the migration lines are "an app that drove `CodeHistory` itself reads the replacements `Undo`
answers" and "a listener of `Changed` reads `change.Edits`, and `change.Replaced` where it read null".

## Capabilities

### Modified Capabilities

- `code-history`: an undo step holds the edits a command made, undo and redo raise each, and a typed
  character joins a run as before.
- `code-lines`: a move is the passed line moving, so the moved lines keep their problems.
- `code-editor`: `Changed` carries a change, the edits an undo or a redo made, and a document replaced
  whole is a change with no edits.
- `code-diagnostics`: a problem travels with an edit the moment the document takes it, so one reported at
  any moment stands where it was reported.
