- **2026-10-18 · The spreadsheet is planned to stand beside Excel and Google Sheets**: Edgar set the
  bar for the shared `Spreadsheet`, and `docs/SPREADSHEET-PLAN.md` lays it out after driving the
  component in Chromium found seventeen defects and reviewing the plan an eighteenth: inside a
  scrolling page the sheet lays out at its full height and its headers scroll away, nothing keeps the
  active cell in view, the column headers drift from their columns in a narrow window, there is no
  horizontal scroll, text arrives only through `keydown`, and a cell key packed into an `int` cannot
  address a row past 131 071. Decided with Edgar: a two-dimensional viewport with pinned bands in the
  vocabulary, Excel's 1 048 576 by 16 384 grid, the cell editor on the code editor's engine, the
  formula bar and the status bar as components of their own, formulas in an opt-in package, and
  interaction before values, formatting and formulas. Twelve slices, each a story under
  [#981](https://github.com/eQuantic/equantic-ui/issues/981), which supersedes #304.
