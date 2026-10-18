- **2026-10-18 · The spreadsheet is planned to stand beside Excel and Google Sheets**: Edgar set the
  bar for the shared `Spreadsheet`, and `docs/SPREADSHEET-PLAN.md` lays it out after
  driving the component in Chromium found seventeen defects: inside a scrolling page the sheet
  lays out at its full height and its headers scroll away, nothing keeps the active cell in view, the
  column headers drift from their columns in a narrow window, there is no horizontal scroll, and text
  arrives only through `keydown`. Decided with Edgar: a two-dimensional viewport with pinned bands in
  the vocabulary, the cell editor on the code editor's engine, formulas in an opt-in package, and
  interaction before values, formatting and formulas. Eleven slices, each a story under
  [#981](https://github.com/eQuantic/equantic-ui/issues/981), which supersedes #304.
