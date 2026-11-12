- **2026-11-12 · A table's cells are scoped by the table's place in the tree**: Bugs under #849
  ([#1115](https://github.com/eQuantic/equantic-ui/issues/1115)) and #1174
  ([#1426](https://github.com/eQuantic/equantic-ui/issues/1426)). On the web a table view's cells, and a
  `Navigable` grid's, took ids scoped by the grid's label, hashed, so two spreadsheets on one page, two
  unlabelled table views (a group's reading order owns its cells too since #1331) or two calendars of one
  month wrote the same ids, and a reader in the second grid heard the first grid's values. The server's
  lowering now follows the tree as the browser's does, by the segments of `LoweringPath`, and the scope is
  the grid's path below its root, read past the step into what a component builds, which the browser takes
  for a component inside a tree and not for a page it mounts as a root: the same on the server's first pass
  and after hydration, as the dashboard's `/sheet` measures.
