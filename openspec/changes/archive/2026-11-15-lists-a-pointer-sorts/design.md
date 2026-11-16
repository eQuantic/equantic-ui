## Context

- `Draggable` (one surface along one axis) is realized on the web by a document-level controller,
  `dom/draggable.ts`: the lowering marks the child with `data-eq-drag*` attributes and handlers for
  `eq-drag-*` custom events, the controller runs the gesture and dispatches those events, and the handlers
  call the C# callbacks. The server declares the same events and runs none (`ClientDispatches`).
- `DropTarget` takes files from the system's drag and drop, on the web only; Photon passes its child through
  and #1216 holds the shells' half.
- Flutter has `ReorderableListView` (`onReorder(oldIndex, newIndex)`), and `Draggable<T>` with `DragTarget<T>`
  (`onWillAcceptWithDetails`, `onAcceptWithDetails`, `onMove`, `onLeave`) for anything across lists.

## How Flutter solves it

`ReorderableListView` reorders ONE list, owns the gap animation and the drag proxy, and reports two indices;
it cannot move an item to another list. Across lists an app composes `Draggable<T>` and `DragTarget<T>` itself
and computes the index from the target's geometry, which each app writes again. dnd-kit, which the Space uses
today, has the shape this change takes: sortable contexts that share a group, items identified by key, and an
over-placement the app reads to draw its indicator.

What this change keeps from Flutter: the drop is the app's to apply (the list is never reordered behind its
back), the guard is asked before a place is offered (`onWillAccept`), and a hover is reported (`onMove`,
`onLeave`). Where it goes further: one node pair does in-list and cross-list moves, the index is computed by
the realizer from the items' boxes, and nesting is a flag. `docs/FLUTTER-PARITY.md` gains a row
(`ReorderableListView`, `Draggable`, `DragTarget`).

## Decisions

### 1. Two nodes and a record, keyed by strings

`Sortable` marks a container, `SortableItem` an item, and an item belongs to the nearest `Sortable` above
it. Both are layout-transparent: a column, a row, a scroller between them, the layout stays the author's.
Items and containers are named by strings, as `DataRow.Key` names a row, because what crosses to the app's
callbacks must be data the app already holds (a card's id), not a node.

### 2. The realizer computes the place; the component draws it

The place an item would land is computed from the items' boxes along the container's axis: before the first
item whose middle the pointer has not passed, with the dragged item left out of the count, so `Index` is where
it lands in the list without it. In a container that `Nests`, the middle half of an item's box means INTO it.
The realizer does not draw an indicator: `OnHovered` reports the place when it changes, and the component
draws its line or gap from the theme, which is how both targets will look the same.

### 3. A pointer starts a drag only when it means to

A mouse starts past 5 px of travel, the Space's own threshold, so a press on a card that does not travel is a
press. A finger starts after holding still for 250 ms, so a swipe still scrolls the list. The item's press
handler never sees the click that ends a drag.

### 4. The drag image is a copy of the item

The controller copies the item's element into a fixed layer that follows the pointer, and dims the item in
place, the dnd-kit overlay the Space draws. The copy is the element as drawn, so it needs no second build.

### 5. Photon is fenced, and says so

The two nodes pass their child through on Photon: the items are drawn and take no drag. #1537 gives the host
the same physics. The vocabulary is the same on both targets, so a board written today moves there unchanged.

## Risks / Trade-offs

- [A copy of the item is a snapshot: a card that changes while dragged shows its old face] → a drag lasts
  seconds, and the item itself, dimmed, shows the live face.
- [Hit testing reads boxes on every move] → only the containers of the item's group and their own items are
  read, and only while a drag is live.

## Migration Plan

Nothing to migrate: every member is added.

## Open Questions

None for this change.
