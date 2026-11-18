- **2026-11-17 · A tree moves its nodes by drag and by keyboard**: with `OnMoved`, a `TreeView` is a nesting
  `Sortable` and each node row a `SortableItem`, so a pointer carries a node before, after or inside another, with a
  line or a ring in `ListTheme.TreeDropColor` saying where, and a closed folder held under it opens after a pause. The
  drop reaches the app as a `TreeMove` (the node, its new parent and the sibling it lands after); the tree refuses a
  place inside a file or inside the node itself, and `CanMove` refuses more. While the app's task runs the node is
  busy, and both parents are read again after. The keyboard moves the active node through the sortable's path, which
  now starts from a composite's active item, steps through the places inside a nesting list's items
  (`SortPlacement.KeyStep`, both targets), passes over refused places, and holds the app's chords while an item is
  carried ([#1533](https://github.com/eQuantic/equantic-ui/issues/1533)). Proposed and archived through OpenSpec
  (`openspec/specs/tree-view`, `openspec/specs/sortable-lists`).
