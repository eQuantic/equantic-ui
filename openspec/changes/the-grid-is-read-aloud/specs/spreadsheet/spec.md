## ADDED Requirements

### Requirement: The grid is read aloud

The spreadsheet's grid SHALL read as a grid named after the sheet, on every target: the column letters
as the headers of their columns, the row numbers as the headers of their rows, the corner as nothing a
reader reaches, and each cell as a grid cell whose name is what it shows, its row and its column those
of the sheet. The cells of every selected range SHALL read as selected and the active cell as current.
The sheet's surface SHALL keep the keyboard, and on the web the grid role SHALL be the table's rather
than the surface's.

#### Scenario: Where a reader finds a cell

- **WHEN** cell B3 holds `14,02` and the grid is realized
- **THEN** the grid's header of column B reads `B`, its header of row 3 reads `3`, and the cell at the
  row of 3 and the column of B reads `14,02`, on the web as on Photon

#### Scenario: The selection and the active cell

- **WHEN** the selection is B2:B4 with B3 active, and D6 is added with ⌘/Ctrl+click
- **THEN** B2, B3, B4 and D6 read as selected and D6 as current, and no other cell reads as either

### Requirement: The grid announces each move and each growing range

The grid SHALL announce, politely and without moving the reader's focus, what changed when the active
cell or the active range changes: when the active range changes and covers more than one cell (Shift
stretching it, a drag drawing it, a header selecting a band), the range's address and its size in rows
and columns; otherwise, when the active cell moves, its address and its value as the cell shows it, or
the word for blank when it is empty. The words SHALL be in the page's culture, as every string of the
SDK is. Nothing SHALL be announced when neither changed, and the grid SHALL announce nothing of its own
while an entry is open in a cell. On Photon an announcement made within a second of the last one SHALL be
said when that second ends, with the grid's state at that time.

#### Scenario: An arrow

- **WHEN** the active cell is B2 and the user presses ↓, B3 holding `14,02`
- **THEN** the grid announces `B3, 14,02`

#### Scenario: A blank cell

- **WHEN** the active cell moves to an empty C3
- **THEN** the grid announces `C3, blank`

#### Scenario: A growing range in Portuguese

- **WHEN** the page's culture is pt-BR, the active cell is B3, and the user presses Shift+→ and then
  Shift+↓ twice
- **THEN** the grid's last announcement is `B3:C5, 3 linhas por 2 colunas`

#### Scenario: A header

- **WHEN** the sheet has 50 rows and the user presses the header of column C
- **THEN** the grid announces `C1:C50, 50 rows by 1 column`

#### Scenario: Quick arrows on Photon

- **WHEN** the user presses → three times within half a second on Photon
- **THEN** the first move is announced at once, the cell the third one reached is announced when the
  second after the first announcement ends, and the second move is never announced

### Requirement: A reader's activation selects what it activates

A reader's activation of a cell SHALL make it the active cell, of a column's header SHALL select the
column, and of a row's header SHALL select the row, as a press there does, on every target.

#### Scenario: Activating a cell

- **WHEN** a reader activates the cell of D6 while the active cell is B3
- **THEN** D6 is the active cell and the selection is D6

#### Scenario: Activating a header

- **WHEN** a reader activates the header of column C
- **THEN** the selection is column C, as a press on that header makes it
