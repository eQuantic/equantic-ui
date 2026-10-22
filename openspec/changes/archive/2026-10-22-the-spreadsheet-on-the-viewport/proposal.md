# Proposal

Closes #995, slice 0b of the spreadsheet plan (`docs/SPREADSHEET-PLAN.md`), a sub-issue of the epic
#981 (a grid that stands beside Excel and Google Sheets).

## Why

The `Spreadsheet` still scrolls one axis, keeps its column headers outside its scroll and paints its
selection on the cells, so the defects measured on 2026-10-18 stand: inside a scrolling page it is as
tall as its rows (D1), a keyboard move leaves the active cell off screen (D2), the headers drift from
their columns at 900 px (D3), columns past the window are unreachable (D4), and a finger on the grid
never scrolls it (D6). Slice 0a (#982) put a two-axis viewport with pinned bands in the vocabulary;
this slice rebuilds the spreadsheet on it, which is what every later slice draws on.

## What Changes

- `Spreadsheet` is rebuilt on `TableView`: the column headers are its pinned first row, the row
  numbers its pinned first column, and the cells are built only where they are in view, on both
  axes, at Excel's extent. A sheet in a scrolling page scrolls inside the room the page shows.
- The selection's band, the active cell's ring, the fill preview and the fill handle are drawn as
  the viewport's marks, over the cells, from rectangles the engine computes, instead of restyling the
  cells. That layer is where the later slices' outlines, ants and guides go.
- The active cell is kept in view after every keyboard move, a paste, a fill, an undo and a redo,
  through the viewport's reveal.
- A selection or fill drag held past the edge of the cells scrolls the grid, faster the further past
  it, and keeps extending. The scroll model gains this (`ITableScrollModel.AutoScroll`), written once.
- The row-number column widens with its digits, measured through the context as the code block's
  gutter is.
- The spreadsheet's colours (gridlines, headers, the range's fill and outline, the active cell, the
  fill preview) come from a theme slot of its own, `IAppTheme.Sheet`, a `SheetTheme` whose every
  member is optional and defaults from the app's foundation tokens. It crosses to the browser in the
  theme's JSON, as the data palette does.
- On the web the sheet takes pointer events: a mouse press selects and a mouse drag stretches, as
  before; a finger's tap selects when it lifts and a finger's drag scrolls the grid. The surface's
  protocol carries the pointer's kind (`ISheetSurfaceModel.HandlePointer(…, PointerKind)`), so the
  engine decides this once for both targets.
- A table viewport may decline the focus (`TableViewport.CanRequestFocus`, and the same on
  `TableView`): the spreadsheet's surface is its one Tab stop, and the keys are its engine's.
- The spreadsheet's components move out of `eQuantic.UI.Components` into an assembly of their own,
  `eQuantic.UI.Spreadsheets`, above it, as the charts did, added by both SDKs with its factories in
  scope (`SpreadsheetsUI`, so `Spreadsheet(controller)` is written as `BarChart(…)` is). The engine
  stays in `eQuantic.UI.Sheet`; the generic `TableView` stays in `eQuantic.UI.Components`.
- **BREAKING** (contract): `Spreadsheet` moves from `eQuantic.UI.Components` to
  `eQuantic.UI.Spreadsheets`, its `HeaderWidth` constant goes (the column widens), and
  `ISheetSurfaceModel.HandlePointer` takes the pointer's kind. `SheetGrid` describes the viewport's
  geometry instead of a window's first cell. Migration: add `using eQuantic.UI.Spreadsheets;` where a
  page names `Spreadsheet` (the factory `Spreadsheet(controller)` needs nothing); a model of one's own
  implementing `ISheetSurfaceModel` adds the `PointerKind` parameter.

## Capabilities

### New Capabilities

None. The spreadsheet's and the viewport's specs already exist and are widened.

### Modified Capabilities

- `spreadsheet`: the grid scrolls on both axes with its headers pinned and aligned by construction,
  the row-number column widens with its digits, the active cell stays in view, a held drag scrolls,
  the marks are drawn over the cells, the colours come from the sheet's theme slot, a finger scrolls
  where a mouse selects, a press is read against the viewport's offset, and the components are an
  assembly the SDK adds.
- `table-viewport`: a viewport may decline the focus, and it scrolls by itself while a drag the
  composing component reports is held past its edge.

## Impact

- **Vocabulary** (`eQuantic.UI.Primitives`, contract): `ITableScrollModel.AutoScroll`,
  `TableViewport.CanRequestFocus`, `SheetTheme` and `IAppTheme.Sheet`, `ISheetSurfaceModel.HandlePointer`
  widened. Their TypeScript twins (`nodes.ts`, `vocabulary.ts`, a theme twin) follow.
- **Engine** (`eQuantic.UI.Sheet`, contract): `SheetGrid` reshaped; the document answers its axes as
  `TableSpans`; the controller reads a point against the viewport's live offset, asks for the active
  cell to be revealed, drives the auto-scroll and decides a touch press.
- **Components**: `TableScroll` (auto-scroll), `TableView` (`CanRequestFocus`); `Spreadsheet` leaves.
- **New assembly** `eQuantic.UI.Spreadsheets` (contract): `Spreadsheet`, its spans adapter and
  `SpreadsheetsUI`; transpiled into the runtime like the charts; `PublicAPI` files, its surface class,
  the package list the packaging check reads.
- **eqc**: the runtime-provided assemblies list (`RuntimeProvidedTypeScanner`) names the new one.
- **SDKs**: `eQuantic.UI.Sdk` and `eQuantic.UI.Sdk.Native` add its package (or project, beside a
  source tree) and its static using; `Sdk.targets` hands eqc its sources.
- **Web realizer and runtime**: the table lowering honours `CanRequestFocus`; the sheet surface moves
  from mouse to pointer events and coordinates with the table's finger pan; the table after-pass ticks
  the auto-scroll; the theme bridge writes `sheet`.
- **Photon**: the host passes the pointer's kind to the sheet and ticks the auto-scroll through the
  tables' clock; `CanRequestFocus` suppresses the table's stop.
- **Samples**: the dashboard's `/sheet` and the gallery's Spreadsheet section use the new assembly.
- **Docs**: the wiki's Components page (its Spreadsheet section) and Package Architecture page in five
  languages, `docs/SPREADSHEET-PLAN.md`, a ledger entry. The developer surface (`developer-surface.baseline.txt`) is unchanged: no setting is added.
