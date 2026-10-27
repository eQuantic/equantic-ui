# Design

## Context

See proposal.md for why. What the change meets, measured on main at 17ee18b13:

- **Photon's semantics are a flat list.** `SemanticNode` is a record in reading order with no parent;
  the macOS, iOS and Android bridges build one flat array of elements under the content view. A
  `TableViewport` announces a `Group` and walks in; a `TableViewCell` is layout (a mark is consumed);
  the `SheetSurface` announces a `CodeField` and walks its children (#1029), so a cell's text arrives
  as `StaticText`. `SemanticRole` has 22 members, `GridCell` the only cell (a calendar day, activatable,
  `AXCell`). `NativeRole.Of` has no default arm, so a role no platform names fails the build.
- **The web writes ARIA inline.** The sheet's host is `role="grid"`; inside it the viewport is
  `role="group"` and its cells are unnamed boxes in four bands (pinned corner, pinned row, pinned column,
  body), each band a clipping box over a layer the after-pass moves by `transform`. A row's cells live
  in two bands. The keyboard lives in the sheet's door, a textarea that stays empty, because an input
  method composes in the element that has the focus when the first key goes down.
- **Announcements.** `LiveRegion` lowers to `role="status"` with `aria-live` on the web. On Photon the
  emit walk records a `LiveRegionMark`, `LiveRegionAnnouncer` diffs the text frame to frame with a
  one-second quiet window, and only the macOS shell drains its queue. Inside the window a change is
  recorded as said and dropped, which contradicts the class's own documentation.
- **Activation.** `PhotonHost.Activate(id)` runs a pressable, follows a link or lands on a focus stop.
  A sheet's cell is none of them: the surface takes the pointer for the whole grid.
- `HtmlElement` already transcribes `aria-rowindex`, `aria-colindex`, `aria-rowcount`,
  `aria-colcount` and `aria-selected`; nothing sets the first four.

## Goals / Non-Goals

**Goals:**

- A `TableView` that reads as a table or a grid on both realizers, for any component, the spreadsheet
  being the first.
- The spreadsheet heard while it is used: every move and every growing range announced, on the web and
  on the three Photon shells that post announcements.

**Non-Goals:**

- Nesting Photon's semantics tree (rows inside tables, cells inside rows): #1105, because every
  container role needs it.
- #1078: the code surface's accessible value and invalid state, the formula bar's and the name box's
  reading, and a failed Go To announced. Its own pull request, next.
- Windows, which has no UI Automation provider (#310).
- A table's default role: a `TableView` stays a group unless it says otherwise.

## Decisions

### 1. The viewport says its role and the cell its part

*How Flutter solves it:* `SemanticsRole` has `table`, `row`, `cell` and `columnHeader`, translated to
each platform's role by the engine, and `TableView`'s cell builder returns a `TableViewCell` that carries
what the cell is. Here the viewport already places `TableViewCell`s, and `TableView` builds them from the
child its builder returns. So the vocabulary gains:

- `TableViewport.Role`, a `TableRole`: `Group` (today's, the default), `Table`, `Grid`.
- `TableViewCell.Role`, a `TableCellRole`: `Cell` (the default), `ColumnHeader`, `RowHeader`, `None`
  (a corner, nothing a reader reaches); `TableViewCell.Selected` and `TableViewCell.Current`.
- `TableView.Role`, and `CellRole` and `Selected` as functions of the row and column, and `Current` as
  a `TableRange` of one cell, which `TableView` writes onto each cell it builds. The builder keeps
  returning the child.

*Alternatives:* the builder returning a `TableViewCell` (Flutter's shape) repeats the row and column
the cell already carries and breaks every builder for no gain; deriving headers from the pinned bands
is wrong for a table whose pinned row is data.

### 2. On the web, rows own their cells across the bands

A row's cells sit in the pinned column's band and the body's band, each clipped and moved on its own,
which is what holds the panes still. Regrouping the DOM by row would undo that. So a `Table` or a `Grid`
viewport writes, as its first child, a zero-size `rowgroup` holding one `row` per built row with
`aria-rowindex` and `aria-owns` naming its cells in column order; every cell carries an `id`, its role
and `aria-colindex`; the bands, the layers and the scrollbars become `role="none"`. The viewport takes
`role="table"` or `"grid"`, its label, `aria-rowcount` and `aria-colcount` (and
`aria-multiselectable="true"` for a grid). Ids follow the `Navigable` grid's rule, a scope hashed from
the viewport's label with the atomizer's FNV, so the server's first pass and hydration write the same
ones (`eq-tcell-{scope}-{row}-{column}`). A `None` cell is `aria-hidden`; marks stay `inert`.

The sheet's host stops claiming the grid role (it keeps its class, its door and its keyboard), and the
spreadsheet names its table with the sheet's label: the grid is the table. The phrase is composed by the
component from the engine's state (decision 4), and what moved is the engine's: `SheetController.Moved`,
read in `Refresh()`, the one call every change of the selection ends with.

*Alternatives:* focusing the grid with `aria-activedescendant` on the active cell is the textbook grid,
and it would move the focus off the door, so the first key of a composition would land outside the
element the input method composes in. The door keeps the focus; the active cell is marked
`aria-current` for a reader browsing the grid, and its moves are announced (decision 4).

### 3. On Photon, new roles in a flat tree

`SemanticRole` gains `Table`, `Grid`, `Cell`, `ColumnHeader` and `RowHeader`, appended (the enum is an
ABI); a grid's cells are the existing `GridCell`. `SemanticNode` gains the cell's place and
the table's extent, both a `TableRange` (the table's own coordinates, which `TableView.Current` already
speaks): a cell's row and column with the rows and columns it spans, which AppKit's index ranges and
Android's `CollectionItemInfo` take as they are, and the whole grid's rows and columns from zero. They are
appended as optional members with every call site moved, and no second constructor. A type of their own
was written first and the value-shape guard refused it: a place in a table is the origin of the range
the vocabulary already had. The web writes a span too (`aria-rowspan`, `aria-colspan`), so both targets
say it. The walk announces the viewport as its
role with its extent, and each cell as its role with its place, `Selected` and `Current`, named by the
text its content holds; the walk does not descend into the plain text it named, and still reaches any
control inside. `NativeRole` names the new roles on the three platforms: `AXTable`/`AXCell` with the
index ranges, `android.widget.GridView` and `CollectionItemInfo`, the header trait on UIKit. Rows are
not emitted until the tree nests.

### 4. The grid's announcement is the surface's own

`SheetSurface.Announcement` is what the grid says now. The `Spreadsheet` composes it, since the engine
does not reach `SdkStrings`: from the active cell (its A1 address and its text, or the word for blank)
when `SheetController.Moved` says the active cell moved, or from the active range (its address and its
size) when it grew around a cell that stayed, with new `SdkStrings` phrases in English, Portuguese and
Spanish. It is a function of the selection, so it stays the same however often the grid builds, and an
entry, which moves neither, leaves it unchanged and announced by nobody. The web writes it into a visually hidden `role="status"` `aria-live="polite"`
`aria-atomic="true"` element inside the host, as the text field's description twin is written. On Photon
the surface's own field carries it: the `CodeField` the walk already announces for the sheet takes the
announcement as its value, so a reader arriving there hears where it is, and is marked live, which is
what Android needs; the emit walk records a live mark carrying the text, so the announcer treats it as
any region and macOS and iOS post it. A separate live node would have needed an id of its own, and the
announcement has no position of its own to take one from.

*Alternative:* the spreadsheet wrapping a `LiveRegion` around hidden text. A `LiveRegion` announces what
its child draws, and the vocabulary has no hidden child on either realizer; the announcement is the
surface's own speech, as its label is its own name.

### 5. The announcer keeps what it could not say yet

A region whose text changes inside its quiet window is marked pending; the first frame at or after the
window's end announces its text then, unless it is back to what was last said. While one is pending the
host keeps asking for frames, as it does while a transition moves, so the window's end is a frame even
when nothing else changes.

### 6. iOS and Android post

iOS drains the queue after each frame and posts `UIAccessibility.Announcement`, queued behind current
speech when polite and interrupting when assertive. Android marks a live `Group` node with
`setLiveRegion` (polite or assertive) and, for each announcement drained, reports
`TYPE_WINDOW_CONTENT_CHANGED` with `CONTENT_CHANGE_TYPE_TEXT` from that node, the route Android keeps
after deprecating announcement events, which Flutter follows too.

### 7. A reader's activation of a cell is a tap at its centre

When `Activate(id)` finds no pressable, link or stop and the node is a grid cell or a header, the host
delivers a primary press and release at the centre of its bounds through the pointer path a mouse
takes. The surface under it answers as it answers a click. On the web a reader's activation is a
synthesized click at the element, which the sheet's pointer handling already receives; the browser
proof checks it.

## Risks / Trade-offs

- [`aria-owns` across bands is read unevenly by readers] → proved with VoiceOver in Safari and in
  Chromium; the cells keep their indices and the announcement carries the active cell even where a
  reader ignores the rows.
- [The flat Photon tree cannot be walked by row or column] → the moves are announced; #1105 ends it.
- [A polite announcement per arrow queues behind speech on the web] → atomic and short; Photon's quiet
  window keeps it to the current state there.
- [Four hundred cells gain an id, a role and an index each] → measured by the runtime's spreadsheet
  bench, whose ceiling holds.
- [A reader's click on a cell may arrive on the web without a pointer press] → the browser proof drives
  VoiceOver's activation; if Safari sends only a click, the sheet answers a click too.
- [iOS VoiceOver does not run in the simulator] → the post is observed in the simulator and heard on a
  device; the pull request says which.

## Migration Plan

Nothing for an app beyond the role moving one element down on the web (proposal, BREAKING).
