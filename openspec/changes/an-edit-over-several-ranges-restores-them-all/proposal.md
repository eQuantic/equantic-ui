# Proposal

Closes #1100, a Bug under #1174.

## Why

With B2 and D4:D5 selected, Delete empties the three cells as one undo step, and undo brings the three
values back, but the selection afterwards is D4:D5 alone. Ctrl+Enter over several ranges does the same.
`SheetEdit` recorded one range before and after the step, the active one, and undo and redo cleared the
others before restoring it and put the active cell at that range's anchor, so a step taken over several
ranges forgot them, and an active cell Tab had walked inside its range went back to the corner.

A paste left the active cell where it was, which could fall outside the range it selected.

## What Changes

- An edit records where the selection stood before and after it as a `SheetSelection`: every range, the
  active one last, and the active cell. Undo and redo restore all of it.
- A paste puts the active cell where it landed, the pasted range's top-left, as Excel does.

## Impact

- `src/eQuantic.UI.Sheet` (`SheetSelection`, `SheetEdit`, `SheetController`) and their transpiled twins.
- `SheetEdit.SelectionBefore` and `SelectionAfter` are `SheetSelection`s, and `SheetController.Fill` takes
  one as its `selectionBefore`: an app that reads or passes them as `SheetRange`s reads `.Range`.
