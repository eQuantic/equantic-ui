# Proposal

Closes #1081, slice 2b of the spreadsheet plan (`docs/SPREADSHEET-PLAN.md`), a sub-issue of the epic
#981 (a grid that stands beside Excel and Google Sheets). Fixes D17. Two pieces the issue named were
split out with Edgar on 2026-10-26: #1078, a code surface's accessible value and invalid state (the
formula bar, the name box and a failed Go To), lands in its own pull request right after this one; and
nesting Photon's semantics tree, which every shell flattens today, is #1105, under #197, because it
serves every container role and not the grid alone.

## Why

A screen reader meets the grid as a field named "Spreadsheet" followed by loose text. Since #1029 the
Photon walk reaches the cells, but as static text: the headers and the filled cells are paragraphs, an
empty cell says nothing, and nothing says which row or column a cell is in, which cells are selected
or which one is active. The web is worse off: the sheet's host claims `role="grid"` over a group of
unnamed boxes, with no row and no cell. An arrow moves the active cell in silence on every target, so
a reader cannot use the grid at all (D17). Two defects stand under any fix: Photon's live region
announcer records a change made inside its one-second quiet window as already said, so the second of
two quick arrows would never be heard, and only the macOS shell posts an announcement; iOS and Android
queue them and post none.

## What Changes

- **A table says what it is.** `TableView` and `TableViewport` gain a role: a group, as today, a
  table, or a grid. A table or a grid reads as rows of cells on every target: on the web `role="table"`
  or `role="grid"` with its rows, `cell` or `gridcell`, `columnheader` and `rowheader`, each with its
  row and column index and the table's full row and column count, so a reader knows where it is in a
  million rows of which forty are built; on Photon the same roles, appended to `SemanticRole`, with the
  cell's row and column on its node.
- **A cell says its part.** A cell is a header of its column or of its row, a cell, or nothing a
  reader reaches (a corner); it may be selected, and one may be current. `TableView` takes them from
  the component that builds the cells.
- **The spreadsheet is a grid.** Its column headers, row headers and cells take those roles, the
  selected ranges' cells are selected and the active cell is current. The sheet's host stops claiming
  the grid role on the web; the table inside it is the grid, and the host keeps the keyboard.
- **The grid says what changed.** `SheetSurface` gains the announcement its model makes when the
  active cell moves or the range grows, and both realizers publish it as a polite live announcement:
  the active cell's address and value as it moves ("B3, 14,02"; "B3, blank"), the range's address and
  size as Shift+arrows grow it ("B3:C5, 3 rows by 2 columns"), in the page's culture (English,
  Portuguese and Spanish, as every SDK string).
- **Every Photon shell announces.** The announcer keeps a change made inside its quiet window pending
  and says the current text when the window opens, as its own documentation already promised. iOS
  posts the platform's announcement and Android marks the region live on its node and reports the
  change, so a `Banner`, a `Toast` and the grid are heard there too.
- **A reader can act on a cell.** Activating a cell or a header that has no handler of its own taps
  its centre, as a pointer would: the cell becomes active, the header selects its column or row. The
  headers regain a role, which slice 2a took away with their pressables.
- **Out of this change:** #1078 (its own pull request, next), nesting the native semantics tree
  (#1105), and Windows, whose shell has no UI Automation provider yet (#310).

For a developer: a `TableView` reads as a table by setting `Role = TableRole.Table`, and as a grid with
`TableRole.Grid`; the builder that makes its cells says which are headers, selected and current. A
`Spreadsheet` is read aloud with nothing to write. **BREAKING** (preview): `SemanticNode` gains members,
so a test that builds one positionally names the new ones; and the web's sheet host no longer carries
`role="grid"`, so a test that found the grid there finds it on the table inside. Migration: look for
the grid's role one element down.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `table-viewport`: a viewport may read as a table or a grid of rows, cells and headers, with each
  cell's place, its selection and the current one, on every target.
- `spreadsheet`: the grid is read aloud: its roles, its selection and active cell, and the
  announcement of each move and each growing range.
- `native-accessibility`: tables, grids, cells and headers in each platform's words, a reader's
  activation of a cell, and a live region announced on every Photon shell, a change made inside the
  quiet window included.

## Impact

- **Vocabulary** (`eQuantic.UI.Primitives`, contract): `TableViewport.Role` and the `TableRole` enum,
  `TableViewCell.Role`, `Selected` and `Current` and the `TableCellRole` enum, `SheetSurface.Announcement`,
  new `SemanticRole` members appended and `SemanticNode` widened. `PublicAPI` files and the enum ABI
  baseline move with them.
- **Components and the spreadsheet**: `TableView` passes the roles through; `SheetController` composes
  the announcement; `Spreadsheet` sets the roles, the selection, the current cell and the announcement;
  `SdkStrings` gains the announcement's phrases in three cultures, and its generated TypeScript twin.
- **Web**: the runtime's lowering and the server's twin for the table viewport and the sheet surface;
  the parity fixture's table and spreadsheet cases change.
- **Photon**: the semantics walk, `NativeRole`, the live region announcer and the emit walk's live
  marks, `PhotonHost.Activate`, and the accessibility bridges of the macOS, iOS and Android shells.
- **Not reached**: eqc, the SDKs, the templates and the developer surface (no MSBuild property,
  configuration section or template parameter moves).
- **Docs**: `docs/SPREADSHEET-PLAN.md` (D17 and the 2b row), `docs/FLUTTER-PARITY.md` (the table
  semantics), the wiki's TableView and Spreadsheet pages in its five languages, and a ledger entry.
