# Proposal

Closes #1100 and #1116, two Bugs under the spreadsheet mission (#981) in what a spreadsheet lost of where the
person was: an undo over several ranges gave back one range (#1100), and a reader's activation of a cell read
where the cell was drawn rather than which cell it was (#1116).

## Why

Delete and Ctrl+Enter over several ranges are one undo step. `SheetEdit` recorded the active range alone as the
selection before and after it, and an undo cleared the other ranges and put the active cell on that range's
anchor: with B2 and D4:D5 selected, an undo brought the three values back and selected D4:D5.

A reader's activation of a cell or a header was answered as a press and a release at the cell's centre, on the
web from the cell's box and on Photon through the pointer path. A row scrolled half under the pinned column
headers has its centre on the header band, so activating it selected a column; an overscan cell's centre lies
outside the window, where the press lands wherever the model maps that point.

## What Changes

- `SheetSelection` records every range, the active one last, and the active cell. `SheetEdit.SelectionBefore`
  and `SelectionAfter` are one, and an undo and a redo restore all of it. `SheetController.Fill` takes one for
  the selection before the fill. The runtime's twin is transpiled from the same source.
- `ISheetSurfaceModel.Activate(row, column)` takes the place a reader activated, in the table's own
  coordinates, its headers counted. The web reads the place from the cell's `data-eq-cell`, and Photon from the
  semantic node's `Place` and the sheet whose surface holds the cell; a grid that is no sheet's keeps the tap at
  its centre.

For a developer: nothing to write. A breaking change of the engine's public surface, in a preview:
`SheetEdit.SelectionBefore` and `SelectionAfter` are `SheetSelection`, and `ISheetSurfaceModel`, which only the
SDK's engine implements, has one more member.

## Capabilities

### Modified Capabilities

- `spreadsheet`: an undo over several ranges restores every range and the active cell; a reader's activation
  reads the cell's place.

## Impact

- `src/eQuantic.UI.Sheet` (`SheetSelection`, `SheetEdit`, `SheetController`) and its transpiled twin in the
  runtime, `ISheetSurfaceModel` in the vocabulary, `PhotonHost.Activate`, and the sheet surface's lowering in
  `lowering.ts`.
