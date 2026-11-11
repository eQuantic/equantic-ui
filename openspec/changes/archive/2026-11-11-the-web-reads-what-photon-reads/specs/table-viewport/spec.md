## MODIFIED Requirements

### Requirement: A viewport may read as a table or a grid

A viewport SHALL read as a group of its cells unless it says it is a table or a grid, on every target.
A group SHALL read its built cells row by row, and in a row column by column, whichever band draws them.
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
- **THEN** it reads as a group, and its cells carry no row, column or count

#### Scenario: A group with a pinned row and column

- **WHEN** a group viewport with its first row and column pinned builds rows 0 to 5 and columns 0 to 3
- **THEN** on the web and on Photon a reader meets the cells of row 0 first, column by column, then those
  of row 1, and so on, never the pinned row's band, then the pinned column's, then the body's
