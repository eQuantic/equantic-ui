## Context

`DataTable` (spec B18) draws a header row and one row per `DataRow`, each a `Grid` over the same column tracks, the
tracks being each `DataColumn`'s `Track` in the order of `Columns`, after a selection column when the table selects.
It is controlled: the app owns the sort, the selection and the data, and the table reports intent. A row's cells are
positional, `Cells[i]` under `Columns[i]`. A `Sortable` with `SortableItem`s puts things in order by pointer and by
keyboard on both targets (#1535, #1536, #1537), each step said in the words on the node (#1564). A `Draggable` with
`Follows = false` and `OnMoved` reports a value that follows the pointer, which a slider already uses; its value is
the rest it started from plus the travel, the rest read once when the gesture starts. An `Adjustable` is one Tab
stop whose arrows report a direction, announced as a slider, a tab list or a radio group.

## Goals / Non-Goals

**Goals:** a person reorders a table's columns by dragging a heading or by the keyboard, and resizes a column by
dragging its heading's edge or by the keyboard, on the web and on Photon; the table reports both and the app keeps
them; a reader hears the handle as a separator with its width.

**Non-goals:** resizing a column whose width the layout decides; hiding or pinning columns; a horizontal scroll for a
table wider than its box; the look of the lifted heading (`SortableTheme`, the next slice); the table's instance
style and builders (#1551).

## Decisions

### How Flutter does it

Flutter's `DataTable` sorts by a heading and does neither of these: its `DataColumn` has a label, a tooltip, `numeric`
and `onSort`, and its widths come from a `TableColumnWidth` the app fixes. An app that wants either composes it, a
`ReorderableListView`-style drag over the headings and a `GestureDetector` on an edge whose `onHorizontalDragUpdate`
adds each delta to a width the app keeps. The packages that do it (`pluto_grid`, `syncfusion_flutter_datagrid`)
report a moved column and a resized one to the app, with a minimum width per column. Here the table composes the
vocabulary's own nodes in the same way and reports the same two facts, so an app writes no gesture.
`docs/FLUTTER-PARITY.md` gains a row for the table.

### The order as positions, the cells read through it

`ColumnOrder` is a list of positions into `Columns`, in the order they are drawn. A row's cells stay in the declared
order, so an app that reorders touches no row; `SortColumn` and `OnSort` keep naming a column by its position in
`Columns`, and so does `OnColumnResized`: the declared position is the column's identity everywhere. A list that is
not a permutation is read leniently: a position out of range or repeated is skipped, and a column it leaves out is
drawn after the others in declared order, so an order kept from before a column was added still draws every column.

`OnColumnsReordered` receives the whole new order, not a move: that is what the app stores, and it is computed by the
same rule the sortable's index is given in, "take it out, insert it at `Index`".

Alternative: reordering `Columns` itself, with the cells keyed by a column id. It moves a new identity into every row
of every app for the sake of one gesture; the positional cells stay.

### The header row is a horizontal sortable

While `OnColumnsReordered` is set, the header grid is the child of a `Sortable` (`Axis = Horizontal`, its own group,
the SDK's words, its name the SDK's word for columns) and each heading cell an item, its id the column's declared
position. The selection cell is not an item, so it stays first. A heading that sorts keeps its press: a mouse that
does not travel sorts, Enter sorts, and Space picks the column up, as on any sortable item.

The table holds the place a carried column would land (`OnHovered`) and draws a line in `ColumnDropColor` at that
place's leading edge, or at the last column's end. A place that would leave the order as it is draws nothing. That
state is why `DataTable` becomes a `StatefulComponent`, as `TreeView` is for the same line.

### A width the table knows, from a handle beside the heading

A drag must start from the column's width. The table knows it only when it is a number, a `Fixed` track, so only
those columns have a handle. Measuring a fluid column would need the laid-out width at build time, which is a
`LayoutBuilder`'s job (#1555, not in yet) or a gesture whose rest the host measures, a new mechanism for one case;
the eQuantic Space list keeps a width for every column it lets a person resize, which is this case, and a fluid
column beside them takes what they leave.

The handle sits in a `Stack` with the heading cell, `Positioned` at its end edge over the full height, 8 dp wide: a
`Draggable` (`Axis = Horizontal`, `Follows = false`, `RestOffset` the width, `Min` and `Max` the column's) whose
`OnMoved` reports the new width live, inside an `Adjustable` (`Role = Separator`, `Value` the width and its range,
named "Resize <heading>") whose arrows step it by 16 dp. It is beside the sortable item, not inside it, so a press on
the handle never picks the column up and Space on it is the handle's. It shows a 2 dp line in `ResizeHandleColor`
when hovered and when focused, and the column-resize cursor.

### A separator role, and the axis it moves on

ARIA's focusable `separator` is the control that moves the boundary between two things (its window splitter
pattern): it requires `aria-valuenow` with its range, as a slider does, and has an orientation, horizontal unless
said. `AdjustableRole.Separator` is appended to the enum (its values are append-only, `EnumValueAbiTests`), lowered as
`separator` when it has a value and `group` when it has none, by the rule the slider already follows. A separator's
orientation is the line it draws, across the axis it moves on, so `Adjustable.Axis` (a `DragAxis`, horizontal by
default) says the axis and each lowering derives the orientation: a separator moved horizontally is
`aria-orientation="vertical"`, a slider moved vertically is `vertical`, and a horizontal slider writes nothing, which
keeps every existing slider's markup as it is.

Photon gives the semantics tree a role for it, `SemanticRole.Splitter`, appended, carrying the value and range as a
slider's does: AppKit's `AXSplitter`, which VoiceOver adjusts, and an adjustable element on iOS and on Android, whose
platforms have no splitter of their own.

### Words

The handle's name and the header list's name are two new `SdkResources` strings in each culture the SDK carries
(`ResizeColumn`, "Resize {0}", and `TableColumns`, "Columns"), reached through `SdkStrings`. The name a sortable
heading already had, "Sort by {0}", was English written in the component; it moves to the same place
(`SortByColumn`), so the heading's three names come from one catalog.

## Risks / Trade-offs

- A table whose fixed columns grow wider than its box overflows it, as it does today with fixed tracks wider than the
  box; a scroll for it is its own change.
- Every move of the handle reports a width, and the app's state change builds the table again, once a frame. That is
  what a slider's `OnMoved` costs, and what the column following the pointer needs.
