# Design

## Context

See proposal.md for the motivation. What the code is today:

- `Spreadsheet` (`eQuantic.UI.Components`) builds a `Column` of a header `Row` over a vertical
  `ScrollView`, whose content is spacers around a window of rows, the row headers a fixed 44-point
  `Column` beside a `SheetSurface` that wraps the window's cells. Every column is built. The
  selection's band, the active ring, the fill preview and the fill handle restyle or decorate the
  cells (`GridRow`). Header presses are `Pressable`s, the resize grips `Draggable`s.
- The engine (`eQuantic.UI.Sheet`) turns a point into a cell with `SheetGrid(Origin, FirstRow,
  FirstCol)`, the window's first cell at a corner of the surface, and loops over the document's sizes
  from there (`CellAt`, `FillHandleCorner`). The document keeps its sizes in two dictionaries.
- The realizers: Photon's `SheetRegion` takes a press no `Pressable`, drag surface or table took
  first, and routes the press's moves to the model until it lifts; the web's `lowerSheetSurface`
  takes `mousedown` and follows with document `mousemove`/`mouseup`, so a finger never reaches it as
  a drag (D6).
- Slice 0a's `TableView`/`TableViewport` (#982, #1014) builds cells lazily on both axes, pins leading
  rows and columns, owns its offset in `TableScroll` on both targets, reveals a range by a held
  request, draws marks over the cells, is a Tab stop and scrolls by the keyboard.
- The theme slots of `docs/ELEMENT-TREE-PLAN.md` §3.10 are designed and not built (E6); `IAppTheme`
  carries default members (`MonoFamily`, `Data`, `Code`), and the bridge writes `data` into the
  browser's theme by hand. A code palette an app overrides does not reach the browser (#936).

## Goals / Non-Goals

**Goals:**

- The spreadsheet on the viewport, at Excel's extent, on both targets, with its headers aligned by
  construction rather than by arithmetic kept in step.
- One reading of a point (cell, fill handle) in the engine, against the viewport's live offset, for
  both hosts.
- The marks layer the later slices draw on, fed by rectangles the engine computes.
- The family's assembly, wired as the charts' is, so slices 1 to 10 land their components there.

**Non-Goals:**

- Resizing through the engine (the grips stay `Draggable`s in the header cells; slice 3 moves them),
  header drags and Shift+clicks (slice 2), the range drawn as one outlined rectangle with the gliding
  cursor (slice 2: this slice moves today's look onto marks unchanged), frozen panes (slice 9).
- A finger on the fill handle: a touch drag scrolls, everywhere on the cells, in this slice; dragging
  the handle by touch arrives with long press in slice 3.
- The theme slots' general mechanism (E6, `Theme(theme, child)`, `With`): this slice adds one slot in
  the shape §3.10 fixes and carries it across as `data` is.

## Decisions

### 1. `Spreadsheet` composes `TableView`, and its surface wraps it

*How does Flutter solve it?* `TableView` (`two_dimensional_scrollables`) with pinned rows and columns
for headers is how a grid is built there, and the parity row `TableViewport` in
`docs/FLUTTER-PARITY.md` names it. The spreadsheet becomes a `SheetSurface` over a `TableView` whose
table row 0 is the column headers, table column 0 the row numbers, and table cell (r + 1, c + 1) the
sheet's cell (r, c), with one pinned row and one pinned column. Headers and cells then share one
layout and one scroll owner: D3 cannot happen and D4 is the second axis.

The surface wraps the whole viewport, headers included, so its coordinates ARE the viewport's.
Alternatives: a surface per cell (a thousand regions, and a drag would cross them), or one around the
body band only (the viewport places only its cells, so there is no node to wrap). Wrapping it keeps
Photon's order of precedence doing the right thing unchanged: a scrollbar press is the table's, a
header press is its `Pressable`'s, a grip drag its `Draggable`'s, and every other press on the cells
is the surface's.

The surface is the sheet's one Tab stop, so the table inside declines the focus
(`CanRequestFocus = false`, below) and the keys stay the engine's: an arrow moves the active cell,
where on a bare table it would scroll. The headers' `Pressable`s decline it too, as Excel's headers
do: a header press selects its band and leaves the keyboard on the sheet. Inside the surface the
readers still meet the headers as buttons and the cells as their values.

### 2. The engine reads a point against the viewport's live offset

A scroll step inside the built window builds nothing (0a's hysteresis), so a geometry handed over at
build time goes stale between builds. `SheetGrid` becomes the viewport's geometry: the scroll model
the component owns (`ITableScrollModel`, read live) and the pinned extents (the header row's height,
the row-number column's width). `CellAt` reads the column and row through the document's axes as
`TableSpans` (`IndexAt`, a binary search) instead of looping from a first cell, and
`FillHandleCorner` is its inverse. The document answers its axes as `TableSpans` adapters over its
size dictionaries, with prefix sums rebuilt when a size changes: `TableSpans` is in the vocabulary,
which the engine already references, and the component wraps them with the header's extent at index 0.
The component owns the scroll and hands it to its `TableView` (`TableView.Scroll`, Flutter's
`ScrollController` handed to a scrollable), so the engine and the viewport read the same object.

A point over a pinned band reads the band's row or column the way the viewport draws it: over the
header row during a drag, the drag holds the first visible row and auto-scrolls (decision 4).

### 3. The marks carry what the cells used to wear

The band's fill, the active ring, the fill preview and the fill handle become `TableViewCell` marks
from the engine's rectangles (the selection, the active cell, `FillTarget`, the handle's cell), in
the colours of decision 6. A cell is then built from its value alone, so a selection change rebuilds
no cell's look and the later slices add marks, never cell styles. 0a already cuts a mark at the pinned
bands, takes no press or focus on it, and keys its pieces. A mark shows no cursor either (the web
draws it `inert`), so the crosshair over the fill handle is the engine's answer,
`ISheetSurfaceModel.CursorAt`, which each host asks over the surface as it asks it what a press means;
the corner counts as the handle only where it is drawn, never under a pinned header.

### 4. Auto-scroll is the scroll model's, driven by the engine and reported back

*How does Flutter solve it?* `EdgeDraggingAutoScroller` (used by `ReorderableList` and
`SelectionArea`): the dragged position is handed to the scroller, which scrolls while the pointer is
past an edge, at a speed that grows with the distance. Here it is
`ITableScrollModel.AutoScroll(Point? pointer)`: `TableScroll` keeps a velocity per axis from how far
the pointer is past the edges of the part that scrolls (the pinned bands count as outside), and
`Advance` moves the offset on the clock both hosts already tick for its glides (`AdvanceTables` on
Photon, `animate` on the web). Null stops it. The engine calls it on every move of a selecting or
filling drag and with null on the release or the cancel.

The grid moving under a still pointer has to keep extending the selection. The viewport reports each
offset (`OnScrolled`); the component hands it to the engine (`SheetController.Follow`), which re-reads
the drag's last position as a move. Alternatives: the engine ticking a clock of its own (a second
clock beside the one that already runs, and a host change on both targets), or the realizer
re-sending the last pointer move each frame (host logic the engine should own).

### 5. The active cell is revealed through the viewport's request

Every engine command that moves the active cell as a consequence of a key, a paste, a fill, an undo
or a redo bumps a reveal version; the component hands
`TableScrollRequest(version) { Reveal = the active cell's table range }` to the `TableView`. 0a holds a
request until the person moves the grid and re-applies it against each layout, so a reveal survives a
layout that measures twice, and it reveals clear of the pinned bands. A press needs no reveal: the
cell is under the pointer.

PageUp and PageDown join the keymap here, moving the active cell by the rows the viewport shows under
its pinned row, because the engine now knows that height and #995's acceptance names the key; the
rest of the page chords (Alt with them, across) stay slice 2's. *How does Flutter solve it?* `Scrollable.ensureVisible` from the focused
cell; the request is the declarative form of the same call, which a write-once component needs.

### 6. The sheet's theme slot, in the shape §3.10 fixes

`SheetTheme`, a record in the vocabulary with every member optional (`ColorToken?` for the gridline,
the header's background and text, the selected header's, the cell's background and text, the range's
fill, the active outline, the fill preview and the handle), and `IAppTheme.Sheet`, a default member
answering null, which is how `Data` and `Code` arrived and how every slot of §3.10 is to. The
component resolves each colour as the slot's member, else its default from foundation tokens (today's
choices: `Border`, `SurfaceSubtle`, `SurfaceHighlight`, `TextMuted`, `TextPrimary`, `Surface`,
`FocusRing`), the precedent `Text.StyleOverride ?? theme.Type(role)` already sets. The bridge writes a
`sheet` object when the slot is not null, as it writes `data`, and the browser's theme answers it; a
theme that never thought about sheets serves the defaults on both targets. *How does Flutter solve
it?* `DataTableThemeData` on `ThemeData`, every member nullable, the widget defaulting from the
`ColorScheme`: the same shape.

### 7. The pointer's kind decides a press, in the engine

*How does Flutter solve it?* `ScrollBehavior.dragDevices`: a touch drags a scrollable, a mouse does
not, and a mouse drag selects. `ISheetSurfaceModel.HandlePointer` gains the pointer's kind. A mouse
or a stylus press behaves as today. A finger's press selects nothing when it lands: its release
selects the cell it began on, and a cancel lands nothing, so a pan the viewport takes (0a's candidate
past a 12-point slop) leaves the selection where it was. Photon already ends the sheet's press when
the table's pan activates; it now says so to the model (`Cancel`). On the web the surface moves from
mouse events and document listeners to pointer events with capture, the table's after-pass marks a
`pointerup` that ended a pan as handled, and the surface reads that mark as a cancel.

### 8. `CanRequestFocus` on the viewport and the table

The vocabulary's word for a node that may decline the focus is `CanRequestFocus`, which `Pressable`
already carries (Flutter's `Focus.canRequestFocus`). `TableViewport.CanRequestFocus` (default true)
removes the viewport's own Tab stop and keyboard on both targets (the stop on Photon, the `tabindex`
and the key handler on the web); `TableView` passes it through.

### 9. The family's assembly is wired as the charts' is

`eQuantic.UI.Spreadsheets`, above `eQuantic.UI.Components` and `eQuantic.UI.Sheet`, with
`SpreadsheetsUI` for its factories (as `ChartsUI` is): both SDKs add it (a `PackageReference`, a
`ProjectReference` beside a source tree, a static `Using`), `Sdk.targets` hands eqc its sources as it
hands the charts', `RuntimeProvidedTypeScanner` lists it so its modules ship inside `runtime.js`, and
it is a `contract` assembly with its own `PublicAPI` files. The name follows `eQuantic.UI.Charts`: a
family, plural, distinct from the engine's `eQuantic.UI.Sheet`; `eQuantic.UI.Spreadsheet` would name a
namespace after the type it holds.

### 10. The row-number column measures its digits

Its width is the widest visible row number measured in the header's type through
`context.MeasureText`, plus padding, as `CodeBlock` sizes its gutter, read from the last row the
viewport shows (`OnScrolled`/`OnViewportChanged`). The column's extent changes only when the digit
count does, and 0a's `AdoptConfig` chooses the window again for the new extents.

## Risks / Trade-offs

- [The column of numbers widens while the grid scrolls, which shifts every cell sideways once a
  digit is added] → Excel does exactly this; the shift happens once per power of ten.
- [A `SheetTheme` before E6 fixes the slots' general mechanism] → it takes the shape §3.10 fixes
  (record, optional data members, a default member answering null); E6 generalizes the crossing and
  `With`, and this slot follows it there.
- [Rebuilding the spreadsheet on every selection change rebuilds the window's cells] → cells no
  longer depend on the selection, so their elements are kept by key; the bench (100 000 rows by 100 columns,
  a keystroke and a scroll step) has a ceiling that fails.
- [Moving `Spreadsheet` breaks every app that names it] → one `using` line, written in the migration
  notes; the factory needs none.
- [On the web, a finger's tap now selects on release, which is a frame later than a mouse's press] →
  it is how a touch platform distinguishes a tap from a pan; Excel on a phone does the same.

## Migration Plan

Lands in one pull request. An app adds `using eQuantic.UI.Spreadsheets;` where it names the type;
one that implements `ISheetSurfaceModel` itself (none in this repository but the engine) adds the
`PointerKind` parameter. Nothing to roll back beyond reverting the merge.
