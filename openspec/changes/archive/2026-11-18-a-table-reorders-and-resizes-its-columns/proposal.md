## Why

Closes #1538, a user story of #1534 (sortable lists and drag and drop), under the epic #1510 (the components eQuantic
Space needs). The list view of eQuantic Space lets a person put its columns in the order they want and give each
the width they want, and keeps both. `DataTable` draws its columns in the order and at the widths the app declared,
and offers neither.

## What Changes

- **The order is the app's, and the table reports a new one.** `DataTable` takes `columnOrder`, the positions of its
  `Columns` in the order they are drawn (null, the declared order), and `onColumnsReordered`, which receives the new
  order after a person moves a column. While the app gives that handler, the header row is a horizontal `Sortable`
  whose items are the column headings: a pointer drags a heading to another place, and the keyboard moves one with
  Space and the arrows, each step said to a reader in the SDK's words, as in any sortable list. A line drawn in
  the theme's colour shows where the column lands. The cells follow their column: a row's cells stay in the declared
  order, and the table reads them through it.
- **The widths are the app's, and the table reports a new one.** `DataTable` takes `onColumnResized`, which receives
  a column's position in `Columns` and its new width in dp. While the app gives it, each column whose track is a width
  (`GridTrack.Fixed`) has a handle at its heading's end edge: a pointer drags it and the width follows live, and the
  keyboard nudges it by 16 dp per arrow. The app keeps the width by giving the column back with that track. A
  column whose width the layout decides (`Flex`, `Auto`) has no handle, since the table does not know where a drag
  would start from. A column keeps between its `MinWidth` (64 dp unless the app says) and its `MaxWidth` (1024 dp).
- **The handle is a separator a reader hears.** A new adjustable role, `AdjustableRole.Separator`, says that a
  control moves the boundary between two things: the web announces it as a focusable `separator` with its value
  and range, and Photon as a splitter (`SemanticRole.Splitter`, AppKit's `AXSplitter`, an adjustable element on iOS
  and Android). `Adjustable.Axis` says which way it moves, so the web says the separator's orientation and a slider
  that moves up and down says it is vertical.
- **A heading is said by its column.** A `SortableItem` takes a `Name`, the name the keyboard's steps say for it
  before what its subtree says, on both targets: a heading that sorts holds a press named "Sort by Amount", and a
  reader moving it hears "Amount". `UI.SortableItem` takes `name`.
- **The theme says the look.** `TableTheme` gains `ColumnDropColor`, the line that shows where a carried column
  lands, and `ResizeHandleColor`, the edge a handle shows under the pointer. Both default to the
  theme's focus ring.
- **`DataTable` holds state.** It becomes a `StatefulComponent` to draw where a carried column lands, kept across
  its parent's builds with the fresh configuration whole; everything it drew before is drawn as it was.

In C#, an app that keeps both:

```csharp
new DataTable(columns, rows)
{
    ColumnOrder = _order,
    OnColumnsReordered = order => SetState(() => _order = order),
    OnColumnResized = (column, width) => SetState(() =>
        columns[column] = columns[column] with { Track = GridTrack.Fixed(width) }),
}
```

Parts reached: the vocabulary (`AdjustableRole.Separator`, `Adjustable.Axis`, `SemanticRole.Splitter`,
`SortableItem.Name`, `TableTheme`'s two colours), the components (`DataTable`, `DataColumn`, `SdkStrings`,
`UI.SortableItem`), the web realizer (C# and TS lowering of an adjustable and a sortable item), the runtime's
sortable controller, the Photon host's keyboard sort, its semantics walk and its role table, the runtime's
design-system twin, and the dashboard sample. The public surface grows; `DataTable` changes its base class, which no app sees except
through `StatelessComponent`'s members, and `DataColumn`'s constructor widens with two optional parameters. Nothing
goes. The developer surface does not move.

Principles followed: 1 (the order and the widths are C# values; the handle's role is the SDK's, never ARIA an app
writes), 4 (written once: the table is composed of `Sortable`, `Draggable` and `Adjustable`, which both realizers
already carry), 5 (the vocabulary names a separator and an axis, not a target's attribute), 13 (signatures widen and
nothing is kept beside them), 15 (the table's new looks are members of its slot record, where a theme sets them).
