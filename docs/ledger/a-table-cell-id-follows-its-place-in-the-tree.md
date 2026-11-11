- **2026-11-11 · A table's cells are scoped by the table's place in the tree**: a Bug under #849
  ([#1115](https://github.com/eQuantic/equantic-ui/issues/1115)). On the web a table's cells, and a
  `Navigable` grid's, took ids scoped by the grid's label, hashed, so two spreadsheets on one page, two
  unlabelled tables or two calendars of one month wrote the same ids, and a reader in the second grid heard
  the first grid's values. The server's lowering now follows the tree as the browser's does, by the
  segments of `LoweringPath`, and the scope is the grid's path below its root, the same on the server's
  first pass and after hydration.
