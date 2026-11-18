# data-table Specification

## Purpose
How a `DataTable` draws its columns in the order the app keeps, lets a person move a column by its heading and
resize one by its edge, and reports both to the app; and the separator a reader hears at a column's edge.

## Requirements

### Requirement: A table draws its columns in the order the app keeps

`DataTable` SHALL draw its columns in the order of `ColumnOrder`, a list of positions into `Columns`, and in the order
of `Columns` when it is null. Each row SHALL draw the cell at a column's declared position under that column. A
position out of range or already drawn SHALL be skipped, and a column the order leaves out SHALL be drawn after the
others, in declared order. `SortColumn`, `OnSort` and `OnColumnResized` SHALL keep naming a column by its position in
`Columns`.

#### Scenario: Two columns swapped

- **WHEN** a table declares the columns `Name` and `Size`, a row's cells are `alpha` and `1`, and `ColumnOrder` is
  `[1, 0]`
- **THEN** the header shows `Size` before `Name`, and the row shows `1` before `alpha`

#### Scenario: An order that leaves a column out

- **WHEN** a table declares `Name`, `Size` and `Kind`, and `ColumnOrder` is `[2, 2, 7]`
- **THEN** the header shows `Kind`, `Name` and `Size`, in that order

### Requirement: A person moves a column, and the table reports the new order

While `OnColumnsReordered` is set, the header row SHALL be a horizontal `Sortable` whose items are the column headings,
each named by the column's declared position, the selection cell not among them. A heading dragged by a pointer or
moved by the keyboard SHALL report the order the drop leaves, the column taken out and inserted at the drop's index,
to `OnColumnsReordered`; a drop that leaves the order as it was SHALL report nothing. While a column is carried, a
line in `TableTheme.ColumnDropColor` SHALL show where it lands. The keyboard's steps SHALL be said in the SDK's words,
the list named by the SDK's word for columns. A heading that sorts SHALL still sort on a press that does not travel
and on Enter. Without `OnColumnsReordered` the header SHALL be no sortable.

#### Scenario: A heading dragged past the next one

- **WHEN** a table shows `Name`, `Size` and `Kind`, and a person drags `Name` and releases it past the middle of
  `Size` and before the middle of `Kind`
- **THEN** `OnColumnsReordered` receives `[1, 0, 2]`

#### Scenario: A column moved by the keyboard

- **WHEN** the focus is on the `Kind` heading of a table showing `Name`, `Size` and `Kind`, and the person presses
  Space, ArrowLeft and Space
- **THEN** `OnColumnsReordered` receives `[0, 2, 1]`, and a reader heard `Kind` picked up and dropped at position 2 of
  3 in Columns

### Requirement: A person resizes a column of known width, and the table reports the width

While `OnColumnResized` is set, each column whose track is `GridTrack.Fixed` SHALL have a handle at its heading's end
edge, beside the heading and not part of its sortable item, showing the column-resize cursor. A pointer dragging the
handle SHALL report the column's declared position and its width, the width it started from plus the pointer's travel,
on every move, kept between the column's `MinWidth` (64 dp by default) and `MaxWidth` (1024 dp by default). ArrowRight
and ArrowUp on the focused handle SHALL report the width 16 dp wider and ArrowLeft and ArrowDown 16 dp narrower,
within the same bounds. A column whose track is `Flex` or `Auto`, and every column of a table without
`OnColumnResized`, SHALL have no handle.

#### Scenario: The handle dragged wider

- **WHEN** the `Size` column, declared second, is 120 dp wide, and a person drags its handle 40 dp to the right
- **THEN** `OnColumnResized` last received `(1, 160)`

#### Scenario: Narrowed by the keyboard to its minimum

- **WHEN** the focus is on the handle of a column 72 dp wide whose `MinWidth` is 64, and the person presses ArrowLeft
- **THEN** `OnColumnResized` receives the column's position and `64`

#### Scenario: A fluid column has no handle

- **WHEN** a table with `OnColumnResized` declares `Name` with a `Flex` track and `Size` with `Fixed(120)`
- **THEN** only `Size` has a handle

### Requirement: The handle is a separator a reader hears

The handle SHALL be an `Adjustable` with `Role = AdjustableRole.Separator`, named "Resize" and the column's heading in
the SDK's words, its value the column's width between its bounds. On the web, an adjustable with the separator role and
a value SHALL be announced as a focusable `separator` with `aria-valuenow`, `aria-valuemin` and `aria-valuemax`, and
with no value as a `group`. Its orientation SHALL be the line across the axis it moves on: a separator moved
horizontally SHALL be `aria-orientation="vertical"`, a slider moved vertically SHALL be `vertical`, and a slider
moved horizontally SHALL write no orientation. On Photon it SHALL be a node of role `Splitter` carrying the value and
range, which AppKit announces as a splitter and iOS and Android as an adjustable element.

#### Scenario: The handle on the web

- **WHEN** a table lowers the `Size` column, 120 dp wide between 64 and 1024, with `OnColumnResized` set
- **THEN** its handle has `role="separator"`, `aria-orientation="vertical"`, `aria-valuenow="120"`,
  `aria-valuemin="64"`, `aria-valuemax="1024"` and the label "Resize Size"

#### Scenario: The handle on Photon

- **WHEN** the same table is laid out on Photon
- **THEN** the semantics tree has a `Splitter` node named "Resize Size" whose range is 120 between 64 and 1024
