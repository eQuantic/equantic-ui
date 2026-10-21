- **2026-10-19 · A table scrolls Excel's whole sheet on both axes, on both targets**: the
  spreadsheet could address 131 071 rows and scroll one axis, because its cell key packed into an
  `int` and its viewport was a vertical list. The vocabulary gains `TableViewport`, a window onto a
  grid with pinned leading rows and columns whose scroll model it carries, and the components gain
  `TableView`, which builds only the cells in view; both realizers own the offset, since Chromium
  draws an element at most 16 777 214 px tall and a million rows of 28 reach 29 million. Every shell
  reads both axes of the wheel, and the cell key is a `long`. Proved in Chromium on the dashboard's
  `/table` and on Photon in the gallery, and benched at 1 048 576 by 16 384: 1.2 ms a step inside the
  window on Photon, 14.6 ms a rebuild
  ([#982](https://github.com/eQuantic/equantic-ui/issues/982)). Proposed and archived through
  OpenSpec (`openspec/specs/table-viewport`).
