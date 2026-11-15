- **2026-11-15 · A tree the keyboard walks, and every target reads**: `TreeView` draws a hierarchy an app has
  or loads, a folder's children loaded through an async loader the first time it opens, with a loading
  item and one that loads again after a failure; it is walked by the WAI-ARIA tree pattern's keyboard,
  type-ahead included, and reveals the path to a node. The vocabulary can state a tree for it:
  `NavigableRole.Tree`, `PressableRole.TreeItem`, an item's `TreePlace` (.NET's own `Level`,
  `PositionInSet` and `SizeOfSet`), `Navigable.OnTextSearch`, and `SemanticRole.Tree` and `TreeItem` on
  every bridge ([#1529](https://github.com/eQuantic/equantic-ui/issues/1529),
  [#1530](https://github.com/eQuantic/equantic-ui/issues/1530)). eqc writes a delegate of two or more
  arguments as that many parameters. Proposed and archived through OpenSpec (`openspec/specs/tree-view`,
  `openspec/specs/screen-reader-parity`).
