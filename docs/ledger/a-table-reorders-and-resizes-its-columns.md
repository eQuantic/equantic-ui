- **2026-11-18 · A table reorders its columns by dragging a heading and resizes one by its edge**: `DataTable` takes
  `ColumnOrder`, the positions of its columns in the order they are drawn, and reports a new one to
  `OnColumnsReordered` after a person drags a heading or moves it with the keyboard, the header row being a horizontal
  `Sortable` that says each step in the SDK's words and draws a line where the column lands. It reports a column's new
  width to `OnColumnResized` while a person drags its heading's edge or steps it with the arrows, for every column
  whose track is a width, between its `MinWidth` and `MaxWidth`. The cells follow their column, the app keeps both,
  and `DataTable` became a `StatefulComponent` to draw the landing. The handle is a separator a reader hears: a new
  `AdjustableRole.Separator`, an `Adjustable.Axis` the web says as the orientation, and `SemanticRole.Splitter` on
  Photon (AppKit's `AXSplitter`); and a `SortableItem` carries the `Name` the keyboard's steps say, so a heading is
  said by its column rather than its press. The dashboard's payments table keeps its order and widths
  ([#1538](https://github.com/eQuantic/equantic-ui/issues/1538)). Proposed and archived through OpenSpec
  (`openspec/specs/data-table`).
