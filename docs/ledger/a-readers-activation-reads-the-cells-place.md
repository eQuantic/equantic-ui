- **2026-10-31 · A reader's activation of a cell reads its place, not its centre**: a Bug ([#1116](https://github.com/eQuantic/equantic-ui/issues/1116)).
  A reader's activation of a spreadsheet's cell or header was answered as a press at the cell's centre, on the web
  and on Photon, and a row scrolled half under the pinned column headers had its centre on the header band, so
  activating it selected a column. `ISheetSurfaceModel.Activate(row, column)` takes the place in the table's own
  coordinates, its headers counted: the web reads it from the cell's `data-eq-cell`, and Photon from the semantic
  node's place and the sheet that holds it. Proposed and archived through OpenSpec (`spreadsheet`).
