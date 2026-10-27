## ADDED Requirements

### Requirement: A viewport may read as a table or a grid

A viewport SHALL read as a group of its cells unless it says it is a table or a grid, on every target.
A table or a grid SHALL read as rows of cells: each row that holds built cells as one row, its cells in
the order of their columns whichever band draws them, the pinned column's first; each cell with its row
and its column in the whole grid, and the rows and columns it spans when it spans more than one; and the
viewport with the number of rows and columns of the whole grid
rather than of the cells it built. On the web a table SHALL be `role="table"` of `row`s and `cell`s and
a grid `role="grid"` of `row`s and `gridcell`s, with `aria-rowcount` and `aria-colcount` on the
viewport, `aria-rowindex` on each row and `aria-colindex` on each cell, counted from one, and
`aria-rowspan` and `aria-colspan` on a cell that spans; the server's first pass SHALL write the same. Marks SHALL stay nothing a reader reaches.

#### Scenario: A grid of Excel's extent on the web

- **WHEN** a grid viewport of 1 048 577 rows of 28 points and 16 385 columns, its first row and column
  pinned, builds the cells of rows 0 to 30 and columns 0 to 12
- **THEN** it is `role="grid"` with `aria-rowcount="1048577"` and `aria-colcount="16385"`, there are 31
  rows, the row of index 3 has `aria-rowindex="4"` and holds its 13 cells with the cell of column 0 first,
  and the cell of row 3 and column 2 has `aria-colindex="3"`

#### Scenario: The same grid on Photon

- **WHEN** the same viewport is realized on Photon
- **THEN** the semantics tree holds a grid that carries the grid's 1 048 577 rows and 16 385 columns,
  and each built cell carries its row and its column

#### Scenario: A table

- **WHEN** a viewport says it is a table
- **THEN** on the web it is `role="table"` of `row`s and `cell`s, and on Photon it is a table of cells

#### Scenario: A cell that spans

- **WHEN** a table's header of row 0 and column 0 spans two columns and its cell of row 1 and column 0
  spans two rows
- **THEN** on the web the header has `aria-colspan="2"` and the cell `aria-rowspan="2"`, on Photon each
  carries the rows and columns it spans, and a cell that spans one row and one column says nothing more

#### Scenario: A group by default

- **WHEN** a viewport says nothing of its role
- **THEN** it reads as a group, as before, and its cells carry no row, column or count

### Requirement: A cell says its part

In a table or a grid, a cell SHALL read as a cell unless it says it is the header of its column or the
header of its row, on every target, and every cell the table holds SHALL be one a reader reaches: the
corner between two headers is the empty header of the row headers' column, and a `TableView` whose
builder returns nothing for a cell SHALL hold it as a blank cell. In a grid, a cell that says it is
selected SHALL read as selected, and the one cell that says it is current SHALL read as current. A
header's name SHALL be what it shows. On the web a header of a column SHALL be `role="columnheader"`, a
header of a row `role="rowheader"`, a selected cell SHALL carry `aria-selected="true"` and the current
one `aria-current="true"`, and a column's header SHALL stand at the same place among the cells its row
owns as each cell of that column among the cells of its own row, since a browser places a cell by that
order rather than by its `aria-colindex`.

#### Scenario: Headers and a corner

- **WHEN** a grid's row 0 holds the headers of its columns, the corner at row 0 and column 0 among them,
  and its column 0 the headers of its rows
- **THEN** the cell of row 0 and column 2 reads as the header of its column and the cell of row 3 and
  column 0 as the header of its row, on every target, the corner reads as a header, and on the web every
  row owns its cells in the same order of columns as row 0 owns its headers

#### Scenario: An empty cell

- **WHEN** a table's builder returns nothing for the cell of row 2 and column 1
- **THEN** that cell reads as a blank cell on every target, and on the web row 2 owns all four of its
  cells, in the order of their columns

#### Scenario: Selected and current

- **WHEN** the cells of rows 2 to 4 of column 2 say they are selected and the cell of row 3 and column 2
  says it is current
- **THEN** those three cells read as selected and the one of row 3 as current, on every target, and no
  other cell reads as either
