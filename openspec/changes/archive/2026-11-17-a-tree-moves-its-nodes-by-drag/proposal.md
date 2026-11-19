## Why

Closes #1533, a user story of #1528 (the TreeView feature), under the epic #1510. eQuantic Space's sidebar moves a
page before, after or inside another by dragging it, as every file tree does (VS Code's explorer, Finder, Notion's
sidebar), and the Space's API takes the moved node, its new parent and the sibling it lands after. `TreeView`
(#1530, #1532) shows, walks, renames and creates; it moves nothing. The sortable drag and drop (#1535, #1536,
#1537) already carries an item by pointer and by keyboard on both targets, and already places an item INTO another
in a list that nests, so the tree is built on it rather than beside it.

## What Changes

- **A node moves by drag.** A tree with `OnMoved` lets a pointer pick a node up as a sortable item and carry it over
  the tree: over the top or bottom quarter of a row it lands before or after it, over the middle half it lands
  inside it. A line at the landing row's indentation, or a ring and a tint on the row it lands inside, says where.
  The drop reaches the app as a `TreeMove`: the node, its new parent (null at the top level) and the sibling it
  lands after (null when it lands first). A node dropped inside another lands as its first child.
- **The tree guards against cycles, and the app guards the rest.** A place that would put a node inside a leaf, or
  inside itself or one of its descendants, is never offered. `CanMove` is asked for every other place: a place it refuses is neither
  shown nor dropped on (across Spaces, say).
- **A closed folder opens under a pause.** A node carried inside a closed node that has children, and held there a
  moment, opens it, loading its children if it must, so the person can drop deeper.
- **The app's task, the tree's patience.** `OnMoved` returns a task. While it runs the moved node is busy; when it
  ends, the children the tree loaded for its old and its new parent are read again, as after a rename.
- **The keyboard moves a node too, through the sortable's keyboard path.** Space on the tree's active node picks it
  up; the arrows step it through every place, before, inside and after each row; Space or Enter drops it; Escape
  puts it back. Each step is said to a reader.
- **Vocabulary, for every composite and every nesting list, not only the tree:**
  - the sortable's keyboard path starts from a composite's ACTIVE item (a tree's, a grid's), whose focus stays on
    the composite's one stop: on the web through `aria-activedescendant`, on Photon through the active cell, as the
    menu key already does (#1532). After a drop the composite keeps the focus;
  - in a `Sortable` that `Nests`, the keyboard's arrows step through the places INSIDE items too, by one arithmetic
    both targets run (`SortPlacement.KeyStep`), and a reader hears "inside" with the item's name.
- **Style.** `ListTheme` gains `TreeDropColor`, the colour of the line and the ring, the theme's focus ring by
  default.
- **Sample.** The dashboard's `/files` tree moves files and folders by pointer and by keyboard.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `tree-view`: a node moves by drag and by keyboard, guarded against cycles and by the app, opening a closed folder
  under a pause, with the app's task reported and the parents read again.
- `sortable-lists`: the keyboard path starts from a composite's active item and keeps the composite's focus after a
  drop; in a list that nests, the keyboard steps through the places inside items and a reader hears them.

## Impact

- **Developer surface (C#):** new `TreeMove` record; `TreeView.OnMoved` and `TreeView.CanMove`, and the same tail on
  the `UI.TreeView` factory; `ListTheme.TreeDropColor`; `SortPlacement.KeyStep`; two SDK strings, `SortMovedInto`
  and `SortDroppedInto`, in the four cultures. Additions only: nothing an app wrote changes.
- **eqc:** nothing; the new members transpile through the existing strategies, and the component pins are written
  again.
- **Runtime (web):** `dom/sortable.ts` starts a keyboard carry from a composite's active item, steps through the
  places inside items in a nesting list, says them, and leaves the focus on a composite after a drop.
- **Photon:** `PhotonHost.Sorting` starts a keyboard carry from the active cell's item and steps through the places
  inside items with the same arithmetic.
- **Developer surface outside C#:** none.
- **Principles (docs/PRINCIPLES.md):** the composite pattern (the tree is a `Sortable` around its `Navigable`, each
  node row a `SortableItem`, with no drag code of its own), one arithmetic for both targets (`SortPlacement`), the
  drop is the app's to apply (`onReorder`'s duty, kept from #1535), and a look an instance needs goes into the
  family's record (`ListTheme`), not onto the component. No rule is bent.
