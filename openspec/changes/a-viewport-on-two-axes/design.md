# Design

## Context

What scrolls today, read in the tree on 2026-10-18 (see proposal.md, Why, for what that costs the
spreadsheet):

- `ScrollView` is one axis everywhere it matters. On Photon its offset is one float in a
  `ScrollState` on its element, moved by the host's `ScrollClock` (glide, fling), applied as a
  layout-time translation in `MeasureScrollView`; `ScrollAxis.Both` is treated as vertical there.
  `ScrollRegion`, `LayoutNode.ScrollRange` and the host's pan are single-axis. On the web it is native
  `overflow`, and `Both` reports only `scrollTop`.
- The shells deliver one wheel delta: macOS reads `scrollingDeltaY`, Windows `WM_MOUSEWHEEL` only.
- `ListView` is lazy in the component: it builds the window from `OnScrolled` and
  `OnViewportChanged` and holds the rest as spacers. No node builds children during layout, and
  `docs/ELEMENT-TREE-PLAN.md` §3.2 keeps it that way until a `LayoutBuilder`-like element kind exists.
- Keeping a thing in view has one precedent: the code editor's `RevealVersion`, which each host
  compares per surface after a frame and answers by scrolling the ancestors the least.
- No size says "the visible extent of the scroller around me": `Fill` falls back to `Hug` on an
  unbounded axis, and the code editor feeds its viewport back by hand.
- Chromium lays an element out at most 16 777 214 px tall (measured in this session), and the
  vocabulary's geometry is `float`, exact for integers only up to 16 777 216.

## Goals / Non-Goals

**Goals:**

- One node and one component that the spreadsheet (0b) and a data table can stand on, with the same
  behaviour on the web and on Photon by construction wherever it can be written once.
- Every number that decides a scroll (the offset, the clamp, the wheel, the pan, the fling, the
  scrollbar) computed in one place, in C#, transpiled to the browser.

**Non-Goals:**

- Migrating `ScrollView` onto the new scroll model. It keeps `ScrollClock` and native overflow; only
  the shells' horizontal wheel reaches it here.
- Keyboard scrolling, focus and grid semantics. The viewport takes no focus; its consumer moves it
  by request (the spreadsheet in 0b, its semantics in slice 2).
- Merged cells and frozen panes beyond the leading bands (slice 8), and a building element kind.

## Decisions

### 1. A node that places, a component that builds

`TableViewport` (vocabulary) places the cells it is handed by row and column, pins the leading
bands, clips, scrolls and draws the marks and the scrollbars. `TableView` (`eQuantic.UI.Components`)
holds the row and column extents and a cell builder, and hands the viewport the cells of the window it
reports, as `ListView` does on one axis.

*How does Flutter solve it?* `TableView` builds a `TableViewport`, a `TwoDimensionalViewport` whose
child manager builds the visible cells during layout. That needs a build in layout, which no node
does here and the Element plan defers to an explicit element kind. The API is Flutter's (spans, a cell
builder, `PinnedRows`, `PinnedColumns`), so when that kind arrives the window can move into it with no
change to an app's code. Rejected: a builder delegate on the node, invoked by each realizer, which
would be the first of its kind, unsupported by the element tree and three realizers' worth of
mechanism for one consumer.

The window has hysteresis: the component builds again only when the visible rows or columns leave the
ones already built, then builds the visible ones plus the overscan. `ListView` rebuilds whenever its
computed window changes, which a scroll of one row always does. Cells are keyed by row and column, so a
cell that stays in the window keeps its element across a rebuild.

### 2. The viewport owns its offset, on both targets

Decided with Edgar. The offset is the node's state, not the browser's: the browser's height cap ends
native scrolling near row 599 000 of 28 px, and a scaled mapping (as ag-Grid stretches its rows) makes
a scrollbar drag skip rows and the end inexact, while Photon would keep a model of its own anyway.
Rejected: native overflow, and native overflow with scaling.

So the web realizer reads the wheel, the pointer and touch itself and draws its own scrollbars. What
the browser gave natively and the design keeps: a trackpad's momentum, which macOS delivers as wheel
movement; an overscroll that passes to the page, by not cancelling the wheel event the viewport could
not take.

### 3. One scroll model, written once

A `TableScroll` class in the vocabulary holds the offset and decides every movement: clamping, the
wheel (pixels, lines and pages, Shift turning a vertical wheel horizontal where the platform did not),
a pan with its velocity over the last moments, a fling decaying on a clock the host supplies, the glide
of a wheel step, the scrollbars' geometry (thumb, track, a drag's mapping, a press's page), the
remainder it could not take for the scroller around it, and the honoured request version. eqc
transpiles it; Photon keeps one on the viewport's element (`NodeStates`), the web one per mounted
viewport. Each host only feeds it events and time. Rejected: a TypeScript twin beside a C# one (the two
drift), and reusing Photon's `ScrollClock` (not written once; it stays for `ScrollView`).

The glide and the fling reuse `ScrollClock`'s constants and its reduced-motion rule, so a wheel feels
the same in a `ScrollView` and a table on Photon.

### 4. Offsets are doubles, positions are relative to the window

1 048 576 rows of 28 px is 29 360 128 px, past what a `float` holds exactly. The model keeps its
offset as two doubles (a `TableOffset` value), the extents contract answers offsets in doubles, and a
cell's position is computed relative to the window's origin in double before it becomes the `float`
the layout uses, a number never larger than the window. On the web, the layer that moves with the
scroll is translated only by the distance scrolled since the window was last built, and the cells sit
at offsets relative to that base, so no CSS length approaches the browser's layout limit.

### 5. Extents are an axis, not a callback per index

`ITableSpans` answers for a whole axis: the count, the extent of one, the offset of one, the index at
an offset, the total. Flutter asks a builder for one `TableSpan` per index; an offset into a million
rows needs prefix sums a per-index callback cannot give. The vocabulary ships two forms: uniform, and
sparse (a default with sorted overrides, offsets in O(log n)). The sheet engine implements the contract
over its document in 0b. The node carries the live object, as `SheetSurface` carries its model.

### 6. Photon: four bands, one region

Measure lays each handed cell out tight to its row's and column's extents, at its offset less the
scroll on the axes that move it: the body on both, the pinned rows sideways only, the pinned columns
vertically only, the corner on neither. Emit draws the body, the pinned columns, the pinned rows and the
corner, each under its own clip and input narrowing, then the marks per band, then the scrollbars in
their strips. A `TableRegion` carries the element and its model to the host, which routes the wheel
(two axes now), the pan and fling, the scrollbar presses and the reveal of a new request version after
the frame, as it reveals a code caret. The offset reaches measure from the element's state, as a
`ScrollView`'s does.

The scrollbars take a strip of their own on each overflowing axis, as Excel's and Sheets' do, so no
cell hides under a thumb and the window the component builds for excludes them.

### 7. The web: layers moved by transform

The node lowers to a box that clips, holding four band layers. The body layer is translated on both
axes, the pinned-row layer sideways only, the pinned-column layer vertically only. A scroll step that
stays inside the built cells writes one transform per layer and touches no cell. The listeners are a
non-passive `wheel` (cancelled only when the model took the movement) and pointer events: a touch pans,
a mouse press is left to whatever the cell holds. The C# server lowering writes the same boxes at offset
zero; hydration adopts them.

### 8. The unbounded axis

Photon: a `ScrollView` measuring its child unbounded on its axis now says how much of it shows, a
visible extent carried down in the constraints; a viewport on an unbounded axis takes it, or the
window's extent with no scroller around. The web: a viewport asked to fill an axis whose box came out
empty (its cells are positioned, so an indefinite parent leaves it at zero) measures the nearest
scrolling ancestor, or the window, and takes that extent, kept current by a `ResizeObserver`. Both
write one development log line naming the viewport and the scroller.

### 9. The shells' second axis

macOS reads `scrollingDeltaX` with `scrollingDeltaY`; Windows handles `WM_MOUSEHWHEEL`, and turns a
vertical wheel with Shift held horizontal. The host's wheel carries both deltas to the topmost region
under the pointer that can move on each axis, and hands the rest to the region around it. A horizontal
`ScrollView` takes the horizontal delta, and a vertical one when there is none, as it does today.

### 10. The cell key

`CellRef.Key` and `CellRef.FromKey` become `long`, and the document's dictionaries key by `long`. In the
browser a `long` is a number, exact far past 1 048 576 × 16 384. The sheet's default extent does not
change here; 0b sets it.

## Risks / Trade-offs

- [The web loses native scrolling's free behaviours: the system's overscroll bounce, scroll-linked
  accessibility, find-in-page scrolling to text] → momentum still arrives from the trackpad; the
  movement the viewport cannot take passes to the page; the grid's own semantics are slice 2; find
  is slice 9, and find-in-page never saw cells outside the window anyway.
- [The web cannot tell a mouse wheel from a trackpad] → the glide applies to line and page deltas and
  to the large, whole-number pixel steps wheels produce; anything else applies as it comes. The rule
  is in the model, so it is tested once.
- [Before hydration, a viewport on an unbounded axis is empty on the web, because the server cannot
  measure] → it was laid out wrong there before (D1); the client corrects it in the frame hydration
  runs, and a test pins that it does.
- [The Element tree's E2b (#976, another session) is changing how the Photon host addresses elements]
  → the Photon part starts from main after E2b merges, or rebases onto it; the region and the state
  follow whatever E2b leaves.
- [The served runtime grows by the model, the lowering and the component] → measured against its budget
  on every push; a growth beyond it is stated in the pull request with the budget's line moved.
- [A new mechanism (a model a host drives) where `ScrollView` has another] → stated as a non-goal to
  migrate now; once the table proves it, `ScrollView` can drive the same model and `ScrollClock` goes.

## Migration Plan

One line for an app that stored a cell's key: hold `CellRef.Key` as a `long`, or keep the `CellRef`.
Nothing else an app wrote changes; `TableView` and `TableViewport` are new.
