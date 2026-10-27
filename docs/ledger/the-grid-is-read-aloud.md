- **2026-10-26 · The grid is read aloud**: slice 2b of the spreadsheet. A `TableView` says what it is to
  a reader (`TableRole`: a group as before, a table or a grid) and what each cell is (`TableCellRole`, and
  `Selected` and `Current` in a grid). The web writes a table or a grid whose rows own their cells across
  the pinned bands by `aria-owns`, each cell with its role, its column and what it spans and the grid with
  its whole row and column count, the server's first pass and the runtime alike; Photon announces five new roles (`Table`,
  `Grid`, `Cell`, `ColumnHeader`, `RowHeader`) with each cell's place and the table's extent, in AppKit's,
  Android's and UIKit's words, over a tree that stays flat until #1105 nests it. The spreadsheet's table is
  that grid, named after the sheet, its headers heading their columns and rows, and its surface announces
  each move and each growing range in the page's culture (`SheetSurface.Announcement`, from
  `SheetController.Moved`): a polite status on the web, its field's value marked live on Photon. A reader's
  activation of a cell or a header is a tap at its centre on both targets. Found on the way and fixed:
  Photon's live region announcer recorded a change inside its quiet window as said, so the second of two
  quick arrows was never heard; only the macOS shell posted an announcement, which iOS and Android now do
  too; and a heading's level reached VoiceOver as garbage on macOS (#1107). The code surface's accessible
  value and invalid state is #1078, next
  ([#1081](https://github.com/eQuantic/equantic-ui/issues/1081),
  [#1107](https://github.com/eQuantic/equantic-ui/issues/1107)). Proposed and archived through OpenSpec
  (`openspec/specs/table-viewport`, `spreadsheet`, `native-accessibility`).
