# Proposal

Closes #1330, a Bug under #1174.

## Why

A `Calendar`, and the `DatePicker` that opens one, was announced on the web as a grid named by its month, whose
weekday captions are column headers, and on Photon as a group of plain text. The calendar's `Navigable` is written
as a grid on the web and was still walked as a group on Photon. A reader on Photon lost the grid's rows and
columns, and the header that says which weekday a day falls on. Found by the semantics parity fixture of #339.

## What Changes

- **Photon reads a `Navigable` as the tree or the grid it says it is**, named by its label, as the web's
  `LowerNavigable` writes `role="tree"` or `role="grid"`.
- **A grid whose first row heads the columns** (`HasHeaderRow`) reads that row's cells as column headers, each
  named by its words: the row content's direct children, the cells the web marks `columnheader`. Its other rows
  are walked as they are, so its days stay the grid cells they say they are.
- The 32 entries of the semantics parity baseline that waited on #1330 are removed. Four entries of the opened
  date picker that also cited #1330 are about the order Photon reads an anchored panel in, not the grid, and are
  re-cited to #1868.

A developer writes nothing different. No public surface and no developer surface moves.

## Impact

- `eQuantic.UI.Native.Components`: the semantics walk.
- The shells read the roles they already map: `Grid` and `ColumnHeader`.
