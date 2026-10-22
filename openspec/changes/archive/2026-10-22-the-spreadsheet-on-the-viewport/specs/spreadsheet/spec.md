## MODIFIED Requirements

### Requirement: A cell sits under its column's header on every target

The spreadsheet's column headers SHALL be its pinned first row and its row numbers its pinned first
column, so every cell starts under the header of its column and beside the number of its row, on every
target, at every width and at every offset on both axes. The row-number column SHALL be as wide as the
widest row number it shows, measured in the header's type, plus padding.

#### Scenario: A grid wider than its window

- **WHEN** a spreadsheet of 26 columns is shown in a window 900 points wide and scrolled across by 300
  points
- **THEN** the left edge of every header shown is the left edge of its column's cells, within half a
  point, on the web as on Photon

#### Scenario: Row numbers that gain a digit

- **WHEN** a spreadsheet of 100 000 rows is scrolled from its first row to row 10 000
- **THEN** the row-number column is wider than it was at the first row, and every cell still starts at
  its right edge

### Requirement: A press on a sheet means the same on every target

The spreadsheet SHALL decide what a pointer means in its engine, from the point in the surface's own
coordinates and the grid's offset at that moment, so the web and Photon answer the same press the same
way. A press of a mouse or a stylus SHALL select the cell under it, a press with Shift SHALL stretch the
selection from its anchor to that cell, the second press of a double click SHALL begin editing the cell
with its value, a press within 8 points of the selection's bottom-right corner SHALL begin a fill, a
plain press that is dragged SHALL move the selection's far corner with the pointer, and the release of
a fill SHALL land it as one step that undo takes back. A press that lands while a cell is being edited
SHALL commit the edit first. A finger's press SHALL select the cell it began on when it lifts, and SHALL
select nothing when the grid takes it as a pan. A press the system cancels SHALL land nothing: a fill
pours nothing, and a drag stops where it stood.

#### Scenario: A press in a scrolled window

- **WHEN** a sheet scrolled down by 40 rows and across by one column is pressed in the second column
  and the third row it shows under its headers
- **THEN** the selection is that one cell, row 42 counted from 0, column C, on the web as on Photon

#### Scenario: The first press on a sheet taller than its view

- **WHEN** a page whose sheet is taller than the scroller around it takes its first press, on B3
- **THEN** B3 is selected, and the scroller has not moved

#### Scenario: A drag from a plain press

- **WHEN** a press on B2 is dragged to D4 and released
- **THEN** the selection runs from B2 to D4 and the active cell is D4

#### Scenario: A fill the system takes away

- **WHEN** B2 holds `7`, its fill handle is dragged to B4, and the system cancels the press before it
  lifts
- **THEN** B3 and B4 stay empty, and undo has nothing to take back

#### Scenario: A press with Shift

- **WHEN** B2 is selected and a press with Shift lands on D5
- **THEN** the selection runs from B2 to D5

#### Scenario: A double click

- **WHEN** the second press of a double click lands on a cell holding `42`
- **THEN** that cell is being edited, and its draft is `42`

#### Scenario: The fill handle

- **WHEN** B2 holds `7`, B2 is selected, and a press on its bottom-right corner is dragged to B4 and
  released
- **THEN** B3 and B4 hold `7`, and one undo empties both

#### Scenario: The fill handle's pointer

- **WHEN** B2 is selected and the pointer rests on its bottom-right corner
- **THEN** the pointer is a crosshair, on the web as on Photon, and over the middle of B2 it is not

#### Scenario: A finger's tap

- **WHEN** B2 is selected and a finger lands on D5 and lifts without moving
- **THEN** the selection is D5

#### Scenario: A finger's pan

- **WHEN** B2 is selected and a finger lands on D5 and drags upward by 200 points before it lifts
- **THEN** the grid has scrolled down and the selection is still B2, on the web as on Photon

## ADDED Requirements

### Requirement: The grid scrolls both axes at Excel's extent

A spreadsheet SHALL build only the cells in view on both axes, SHALL reach the last row and the last
column of its document up to Excel's 1 048 576 rows by 16 384 columns, and SHALL scroll its cells under
its pinned headers by the wheel, a trackpad, a finger and its scrollbars, as a table viewport does. A
spreadsheet in a page that scrolls SHALL take the room the page shows and scroll inside it.

#### Scenario: The last cell of Excel's grid

- **WHEN** a sheet of 1 048 576 rows by 16 384 columns is scrolled to its end on both axes
- **THEN** its last cell is drawn under the header `XFD` and beside the number `1048576`, on the web as
  on Photon

#### Scenario: A sheet in a scrolling page

- **WHEN** the dashboard's sheet screen, in its scrolling page, is scrolled down by the wheel over the
  sheet
- **THEN** the rows move under the column headers, which stay at the top of the sheet

### Requirement: The active cell stays in view

After a key moves the active cell, and after a paste, a fill, an undo or a redo, the spreadsheet SHALL
scroll, at once, until the active cell lies in the part of the grid its headers leave uncovered, on
every target. PageDown and PageUp SHALL move the active cell by the rows the grid shows under its
headers.

#### Scenario: Forty rows down

- **WHEN** ↓ is pressed forty times from A1 in a sheet whose window shows twenty rows
- **THEN** A41 is the active cell and lies inside the window

#### Scenario: The sheet's last cell

- **WHEN** ⌘↓ then ⌘→ (Ctrl on Windows) are pressed in a sheet of 1 048 576 rows by 16 384 columns
- **THEN** the active cell is its last cell and lies inside the window

#### Scenario: A page down

- **WHEN** PageDown is pressed from A1 in a sheet whose window shows twenty rows under its headers
- **THEN** A21 is the active cell, and it lies inside the window

#### Scenario: An undo out of view

- **WHEN** B2 is edited, the grid is scrolled until B2 is out of view, and the edit is undone
- **THEN** B2 is the active cell and lies inside the window

### Requirement: A held drag scrolls the grid

A drag that selects or fills SHALL scroll the grid while it is held past the edge of the part where
the cells scroll, faster the further past that edge it is, and the selection or the fill SHALL keep
reaching the cells that come into view under it. The release or the cancel SHALL stop the scroll.

#### Scenario: A selection dragged below the grid

- **WHEN** a press on B2 is dragged 60 points below the bottom edge of the grid and held for one second
- **THEN** the grid has scrolled down, the selection runs from B2 to a row past the ones first shown,
  and after the release the grid stops

#### Scenario: Further is faster

- **WHEN** a selection drag is held 30 points below the grid for one second, and another 120 points
  below for one second
- **THEN** the second has scrolled further than the first

### Requirement: The selection is drawn over the cells

The selection's band, the active cell's ring, the fill's preview and the fill handle SHALL be drawn
above the cells, over the rectangles they cover, and a cell SHALL be drawn from its value alone, on
every target. Where one crosses a pinned header it SHALL be cut there as the viewport's marks are.

#### Scenario: A range selected

- **WHEN** B2:D4 is selected
- **THEN** the cells B2 to D4 are drawn as every other cell is, and the band and the active ring are
  drawn above them

### Requirement: The sheet's colours come from its theme slot

The spreadsheet SHALL take its gridlines, its headers' colours, its cells' colours, the range's fill,
the active outline, the fill's preview and the handle from the app theme's sheet slot where the slot
names them, and from the theme's foundation colours where it does not, on every target.

#### Scenario: A theme that names a gridline

- **WHEN** an app's theme names the sheet's gridline colour
- **THEN** the gridlines are drawn in it, on the web as on Photon

#### Scenario: A theme that names nothing for sheets

- **WHEN** an app's theme answers no sheet slot
- **THEN** the gridlines are drawn in the theme's border colour and the headers in its subtle surface

### Requirement: The spreadsheet's components are an assembly the SDK adds

An app built with either SDK SHALL reach the spreadsheet's components through the
`eQuantic.UI.Spreadsheets` namespace, and their factories with no `using` at all, with no package
reference of its own; `eQuantic.UI.Components` SHALL declare none of them.

#### Scenario: A page that shows a sheet

- **WHEN** a page in a project whose file names only the SDK builds `Spreadsheet(new SheetController(rows: 200, cols: 8))`
- **THEN** it builds, and the sheet takes clicks and keys in the browser and on Photon

#### Scenario: The component library's own types

- **WHEN** the types of `eQuantic.UI.Components` are listed
- **THEN** none of them is the spreadsheet
