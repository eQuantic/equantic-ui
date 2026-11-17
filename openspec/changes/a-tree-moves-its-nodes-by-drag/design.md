## Context

`TreeView` draws its visible nodes as one flat list of rows inside a `Navigable` with the tree role, whose one stop
is the host and whose active item is followed by key. The sortable drag and drop (`Sortable`, `SortableItem`,
`SortDrop`, `SortPlacement`) carries an item over a list by pointer and by keyboard on the web and on Photon, reads
the items' boxes itself, asks the list's `Accepts`, reports `OnHovered` and `OnDropped`, and in a list that `Nests`
places an item INTO another over the middle half of its box. Its keyboard path starts from the focused element and
steps by whole positions only.

## How Flutter solves it

`TreeSliver` has no drag and drop; an app composes `Draggable` and `DragTarget` per row and measures each target
itself, and `ReorderableListView` reorders a flat list with `onReorder` left to the app (docs/FLUTTER-PARITY.md, the
`ReorderableListView` row). The duties are kept: the drop is the app's to apply, a guard is asked first
(`onWillAccept`), and the hover is reported (`onMove`). What Flutter leaves to each app, the tree does once.

## Decisions

### The tree is a `Sortable` around its `Navigable`

With `OnMoved` set, the tree wraps its `Navigable` in a `Sortable` that nests (vertical, its `Label` the tree's),
and each NODE row in a `SortableItem` whose id is the node's key. Status rows (loading, retry) and the creation
field are not items, so the sortable's index counts node rows only. With no `OnMoved`, nothing is wrapped and the
tree is drawn exactly as before. A busy node, and every node while a field is open, is a disabled item.

Rejected: a drag of the tree's own on `Draggable` and `DragTarget`. It would repeat the gesture core, the lift, the
edge scroll, the keyboard path and the announcements the sortable already has on both targets.

### A drop becomes a `TreeMove`

The sortable reports a `SortDrop` whose `Index` counts the list without the carried row. The tree reads it against
the node rows without the carried one (`others`):

- `Into` set: the new parent is that node, and the node lands FIRST (`After` null). First, not last, because the
  children of a closed folder may not be loaded and "last" would need them; and because the geometry agrees: below
  the middle half of an open folder's row is the place before its first child, which is the same place.
- `Index` < `others.Count`: the node lands before `others[Index]`, so its new parent is that row's parent and `After`
  is the nearest earlier row with the same parent, or null.
- `Index` = `others.Count`: after the last row, so its parent is the last row's parent and `After` is that row.

A drop at the place the node already holds (same parent, same `After`) is no move and calls nothing.

### The cycle guard is the tree's, the rest is the app's

`Accepts` refuses a move whose new parent is a leaf (the sortable nests into every item, and a file holds nothing),
or the node itself or one of its descendants, found by walking the visible rows' parents up from the new parent;
then it asks `CanMove`. A refused place is neither drawn nor dropped
on, as the sortable already promises.

### The indicator is drawn on the row, never between rows

`OnHovered` keeps the hovered move. The row a node would land before draws a line along its top edge, starting at
that row's indentation; after the last row, along its bottom edge; the row it would land inside draws its ring in
`TreeDropColor` over the theme's selection tint. The line is a `Positioned` box in a `Stack` over the row's surface,
so no row changes size or moves while the pointer reads the rows' boxes: an inserted row would shift every row below
it under the pointer, and a border that appears would move the row's content.

### A closed folder opens under a pause

When the hovered move lands inside a closed node that has children, the tree waits 700 ms (Finder's spring-loading
is of that order) and, if the same place is still hovered, opens it the way a press does, loading its children. A
different place, a drop, or the end of the drag forgets the wait. The rows read again on the next move, on both
targets, so the opened children take places at once.

### The keyboard starts from a composite's active item

A tree's focus is on its host, never on an item (the host is the one stop, `aria-activedescendant` names the active
item). The keyboard's sort, like the menu key since #1532, starts from the composite's active item when the focus is
on a composite: on the web the controller resolves `aria-activedescendant` inside the focused host; on Photon the host
resolves the active cell's item (`ActiveItemOf`). After a drop the composite keeps the focus, and the tree's active
item, followed by key, is the moved node again.

### In a nesting list the keyboard steps through the places inside items

`SortPlacement.KeyStep(index, into, step, others, nests)`: in a list that nests, the places are, in order, before the
first item, inside it, before the second, inside it, and so on to after the last (2n + 1 places for n other items);
an arrow takes the next or the previous, kept inside the list. In a list that does not nest it is `Step`, as before.
Both targets run it. A place inside an item is said with its name ("{0} is inside {1}", "{0} dropped inside {1}"),
from two new SDK strings.

## Risks / Trade-offs

- The keyboard in an open folder reaches the same place twice (inside the folder, then before its first child). It
  is the same move, said two ways; skipping it would make the arithmetic know about trees.
- Photon still does not speak the keyboard's steps (#1564); the places are the same, the words come with that slice.
- Between the last child of an open folder and the folder's next, shallower sibling there is one index, read as
  "before the sibling": a node becomes the last child of an open folder only at the tree's end. Reading the depth
  from the pointer's position across the axis, as Notion's sidebar does, changes what a sortable reports on both
  targets, so it is a slice of its own (#1594).
- A drop inside a folder lands first, which an app that sorts its folders by name does not mind, and an app that
  orders by hand sees where it went.
