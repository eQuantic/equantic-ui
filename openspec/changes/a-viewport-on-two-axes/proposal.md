# Proposal

#982, slice 0a of `docs/SPREADSHEET-PLAN.md`, a story under feature #981 (the spreadsheet that stands
beside Excel and Google Sheets).

## Why

A spreadsheet is a window onto a grid far larger than any screen, with its headers held still on two
edges, and the vocabulary has no way to say that. `ScrollView` scrolls one axis: `ScrollAxis.Both` is
measured, clamped and panned as vertical on Photon, and on the web it reports only `scrollTop`.
`Pinned` holds a header to the top only. So the shared `Spreadsheet` fakes it: column headers outside
the scroll, row numbers inside it, every column built. Driving it in Chromium on 2026-10-18 measured
the cost. In a narrow window the headers drift 26 px per column from their cells (D3). Columns past
the window cannot be reached (D4). Inside a scrolling page the sheet lays out at its full height and
its headers scroll away (D1).

The browser's own scroll cannot carry the grid either. Chromium caps an element at 16 777 214 px
(measured), about 599 000 rows of 28 px, and Excel has 1 048 576. Separately, the engine packs a cell
address into an `int` that overflows at row 131 072 in .NET and not in JavaScript (D18). This slice
gives the vocabulary the viewport the spreadsheet and every later slice stand on. Slice 0b (#995)
rebuilds the spreadsheet on it.

## What Changes

- **A new vocabulary node, `TableViewport`**, as Flutter's `TableViewport`: a window onto a grid of
  rows and columns whose extents it is given, with `PinnedRows` and `PinnedColumns` held still on the
  leading edges and the corner held on both. It scrolls on both axes and OWNS its two-dimensional
  offset on every target (decided with Edgar, 2026-10-18), so the browser's height cap never limits
  the grid. Wheel, trackpad, Shift+wheel and touch move it, a touch fling glides, and a scrollbar on
  each axis it overflows can be dragged or pressed. It reports where it is and how large its window
  is. A versioned request scrolls it to an offset or reveals a range of cells by the least movement.
  Marks drawn over a range of cells (a selection's outline, later the ants and the guides) are placed
  from the same extents.
- **A new component, `TableView`**, in `eQuantic.UI.Components`, as Flutter's `TableView`: the
  extents of its rows and columns, a cell builder, `PinnedRows`, `PinnedColumns`, and an overscan. It
  builds only the cells in view plus the overscan, from the viewport's reports, the way `ListView`
  does on one axis. A grid of 1 048 576 rows by 16 384 columns costs what a screenful costs. The
  factory `TableView(…)` is in scope everywhere, as every component's is.
- **The unbounded axis**: a viewport asked to fill an axis that has no bound (a sheet in a scrolling
  page) takes the visible extent of the nearest scroller around it on that axis and scrolls inside
  it. A development log says what was decided. Flutter answers the same layout with an error; the
  SDK already knows the answer, so the app writes nothing.
- **The Photon shells deliver a horizontal wheel**: macOS reads `scrollingDeltaX` beside
  `scrollingDeltaY`, Windows handles `WM_MOUSEHWHEEL`, and Shift with a vertical wheel scrolls
  horizontally where the platform leaves that to the app. A horizontal trackpad swipe reaches a
  horizontal `ScrollView` too.
- **A cell address holds at Excel's extent**: `CellRef.Key` becomes a `long` (**BREAKING** for a
  caller that stored the key as an `int`). The migration line: hold `CellRef.Key` as a `long`, or
  keep the `CellRef` itself.

## Capabilities

### New Capabilities

- `table-viewport`: the two-dimensional viewport and its lazy table. Pinned bands, scrolling on both
  axes on every target, the reports and the requests, marks over ranges, the unbounded axis, and a
  table that builds only what is in view.
- `scrolling`: how wheel and trackpad movement reaches a scroller on Photon, on the axis it moves.

### Modified Capabilities

- `spreadsheet`: a cell's address round-trips at Excel's extent on every target (D18).

## Impact

- **Vocabulary** (`eQuantic.UI.Primitives`): `TableViewport` and its value types (cells, marks, the
  scroll request), the `ITableSpans` extents contract with its uniform and sparse forms, and the
  write-once two-dimensional scroll model (offset, clamping, wheel, pan, fling, scrollbar geometry)
  that both hosts drive. The visitor gains a `Visit`; `ChildSlots` and `LayoutTransparency` gain
  their cases. The public surface grows; nothing goes.
- **Photon** (`Native.Framework`, `Native.Components`, the macOS and Windows shells): measure with
  the pinned bands and the unbounded-axis rule, emit with one clip per band and the scrollbars, a
  region and its routing in the host (wheel on two axes, pan, fling, reveal by version), the
  horizontal wheel in both shells, semantics. Experimental surfaces (`[EQX0001]`) move.
- **Web**: the TypeScript lowering (layers moved by transform, wheel, pointer and touch, scrollbars,
  reveal, the unbounded rule by `ResizeObserver`) and its C# SSR twin; the runtime's twins
  (`nodes.ts`, `vocabulary.ts`, the generated node kinds, the exports fixture). The served runtime
  grows, within its budget or with the budget's line moved and said.
- **Components**: `TableView`, its factory in `UI`, its transpiled pin.
- **Engine** (`eQuantic.UI.Sheet`): `CellRef.Key` and `FromKey` move to `long`; the document's
  dictionaries key by `long`; the transpiled pins move.
- **eqc**: an array literal of a vocabulary enum's values is typed by the enum's union. It was a
  `string[]` to TypeScript, so the scroll model's loop over its two axes failed the runtime's
  typecheck: fixed in general and pinned (`EmittedTypeSurfaceTests`), as a gap the new code shows is.
- **`PointerKind`** moves from `eQuantic.UI.Native.Components` into the vocabulary and gains
  `Stylus` (**BREAKING** for a caller of the experimental native surface: the namespace changes).
- **Developer surface** (`developer-surface.baseline.txt`): none. No setting, section or template
  parameter moves.
- **Docs**: the wiki's components page and a `TableView` page in five languages, the
  `FLUTTER-PARITY.md` rows for `RenderSliver` and the two-dimensional viewport, the plan's status, and
  a ledger entry.
