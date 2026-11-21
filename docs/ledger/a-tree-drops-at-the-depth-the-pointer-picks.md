- **2026-11-21 · A tree drops a node at the depth the pointer picks**: a `Sortable` whose `Indent` is above 0 has
  items with depths (`SortableItem.Depth`), an item carries the deeper items right after it, and a place between two
  items reports in `SortDrop.Depth` the depth the carried item takes there, from the depth of the item after the place
  to one deeper than the item before it. A pointer asks for one depth per `Indent` it travelled across, the keyboard's
  cross arrows step it before moving to the neighbouring list, a refused depth gives way to the nearest one, shallower
  first, and a reader hears the position among the items at that depth under their holder. `SortPlacement` holds the
  arithmetic, which the browser's controller and the Photon host both run. `TreeView` gives its rows their depths, so a
  node lands as the last child of an open folder or among the shallower rows after it, and the line follows the depth
  ([#1594](https://github.com/eQuantic/equantic-ui/issues/1594)). Proposed and archived through OpenSpec
  (`openspec/specs/sortable-lists`, `openspec/specs/tree-view`).
