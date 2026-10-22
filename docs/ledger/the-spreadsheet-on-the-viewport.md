- **2026-10-22 · The spreadsheet scrolls both axes on the table viewport**: the sheet moved into an
  assembly of its own, `eQuantic.UI.Spreadsheets`, and onto slice 0a's viewport, its headers pinned
  rows and columns of the table, so a cell starts under its header by construction and only the cells
  in view are built, at 100 000 × 100 in the gallery and up to Excel's extent. The engine reads a
  press against the live scroll, keeps the active cell in view through a reveal version that carries
  the cell the command left, and scrolls a held drag; the selection is drawn as marks, the colours
  come from the theme's new sheet slot, and the web surface takes pointer events, a finger's tap
  selecting on release and its pan scrolling. Measured in Chromium (headers aligned to 0 px at 400, 900
  and 1 440 px; a drag held below the grid scrolled 1 383 px in a second and stopped on release) and
  benched at 100 000 × 100: a keystroke 2.6 ms on Photon and 9.1 ms in the runtime
  ([#995](https://github.com/eQuantic/equantic-ui/issues/995)). Proposed and archived through OpenSpec
  (`openspec/specs/spreadsheet`, `openspec/specs/table-viewport`).
