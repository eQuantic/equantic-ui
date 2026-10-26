## ADDED Requirements

### Requirement: Tables, grids, cells and headers in each platform's words

A table and a grid SHALL reach the native tree as roles of their own, and so SHALL a cell, a grid cell,
the header of a column and the header of a row, each in the words its platform uses: on macOS a table
and a grid are `AXTable` and their cells and headers `AXCell`, with the cell's row and column index
ranges set; on Android a table and a grid are `android.widget.GridView` and a cell carries its row and
column; on iOS a header carries the header trait and a grid cell the button trait, as a calendar day
does. A table, a grid and a header SHALL be neither activatable nor adjustable, and a grid cell SHALL be
activatable. A new role SHALL fail the build until every platform names it. The tree stays flat in this
change: a cell is not nested inside its row or its table.

#### Scenario: A grid on macOS

- **WHEN** a grid viewport holds the cell of row 3 and column 2 and is realized in the macOS shell
- **THEN** AppKit reads an element whose role is `AXTable`, and one whose role is `AXCell` with the row
  index range starting at 3 and the column index range starting at 2

#### Scenario: Headers on iOS

- **WHEN** a grid's row 0 holds the headers of its columns and is realized in the iOS shell
- **THEN** each header's element carries the header trait

### Requirement: A reader's activation of a cell lands where a tap would

A reader's activation of a grid cell or of a header that has no handler of its own SHALL be a primary
press and release at the centre of its bounds, delivered as a tap there would be, so that the surface
under it answers as it answers a tap.

#### Scenario: A cell under a surface

- **WHEN** a reader activates a grid cell drawn inside a surface that handles the pointer
- **THEN** the surface receives a press and a release at the cell's centre, and nothing else is pressed

### Requirement: A live region is heard on every shell

A live region whose text changes SHALL be announced by the macOS, iOS and Android shells alike, with its
urgency: macOS posts an announcement request, iOS posts the platform's announcement, and Android marks
the region's node live and reports that its content changed. A region that changes within a second of
its last announcement SHALL be announced when that second ends with its text at that time, once,
however many times it changed in between, and not at all if its text is back to what was last said.

#### Scenario: A change inside the quiet window

- **WHEN** a live region announces `B3, 1` at 0 milliseconds, says `C3, 2` at 300 and `D3, 3` at 600,
  and nothing after
- **THEN** `D3, 3` is announced on the first frame at or after 1 000 milliseconds, and `C3, 2` is never
  announced

#### Scenario: Back to what was said

- **WHEN** a live region announces `B3, 1` at 0 milliseconds, says `C3, 2` at 300 and `B3, 1` at 600
- **THEN** nothing more is announced

#### Scenario: Android

- **WHEN** a polite live region's text changes in the Android shell
- **THEN** its node is a polite live region and the shell reports a change of that node's content
