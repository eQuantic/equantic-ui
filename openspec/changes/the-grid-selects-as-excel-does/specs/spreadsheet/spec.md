## ADDED Requirements

### Requirement: Several ranges are selected at once

The spreadsheet SHALL hold a selection of one or more ranges, the last of which is the active range and
holds the active cell, and SHALL draw every one of them, on every target. A press with ⌘ (Ctrl on
Windows and Linux) SHALL add the cell under it as a new range and make it active, and dragging that
press SHALL stretch the new range; Shift+F8 SHALL enter add mode, in which a press or a drag adds a range
as it would with ⌘ or Ctrl, until Shift+F8 is pressed again or Escape is. A press without a modifier, an
arrow, Go To and every other move that selects SHALL leave one range. Copy, cut, paste, the fill down
and right chords and the fill handle SHALL act on the active range alone; Delete SHALL clear every range,
and Ctrl+Enter (⌘↩) SHALL fill every range with the entry, as one step undo takes back.

#### Scenario: A press with Command adds a cell

- **WHEN** B2:C3 is selected and a press with ⌘ (Ctrl on Windows) lands on E5
- **THEN** the selection is B2:C3 and E5, E5 is the active cell, on the web as on Photon

#### Scenario: A drag with Command adds a range

- **WHEN** B2 is selected and a press with ⌘ on D4 is dragged to E6 and released
- **THEN** the selection is B2 and D4:E6, and E6 is the active cell

#### Scenario: Add mode

- **WHEN** B2 is selected, Shift+F8 is pressed and a plain press lands on D4, then Escape is pressed and
  a plain press lands on F6
- **THEN** after the first press the selection is B2 and D4, and after the second it is F6 alone

#### Scenario: Delete clears every range

- **WHEN** B2 and D4:D5 hold values, B2 and D4:D5 are selected, and Delete is pressed
- **THEN** B2, D4 and D5 are empty, and one undo restores all three

#### Scenario: Copy takes the active range

- **WHEN** B2 holds `1`, D4 holds `2`, B2 and D4 are selected, and the selection is copied
- **THEN** the clipboard holds `2`

#### Scenario: Ctrl+Enter fills every range

- **WHEN** B2 and D4:D5 are selected, `x` is typed and Ctrl+Enter (⌘↩) is pressed
- **THEN** B2, D4 and D5 hold `x`, and one undo empties all three

### Requirement: A header answers the pointer through the engine

The spreadsheet SHALL read a press on its headers in its engine, with the press's modifiers, on every
target. A press on a column header SHALL select that whole column with its first cell shown active, a
drag from it across column headers SHALL stretch the band to the column under the pointer, a press with
Shift SHALL stretch the band from the anchor's column to the pressed one, and a press with ⌘ (Ctrl on
Windows) SHALL add the column as a range; a row header SHALL answer the same along rows, and the corner
where they meet SHALL select the whole sheet. A finger's press on a header SHALL select when it lifts, as
on a cell. A press on a header's resize grip SHALL resize and select nothing.

#### Scenario: A drag across column headers

- **WHEN** the header of column B is pressed and dragged to the header of column D, then released
- **THEN** the columns B to D are selected, whole, on the web as on Photon

#### Scenario: Shift on a row header

- **WHEN** B3 is selected and a press with Shift lands on the header of row 6
- **THEN** the rows 3 to 6 are selected, whole

#### Scenario: Command on a column header

- **WHEN** B2 is selected and a press with ⌘ (Ctrl on Windows) lands on the header of column E
- **THEN** the selection is B2 and the whole column E

#### Scenario: A resize grip

- **WHEN** B2 is selected and the grip on the right edge of column C's header is dragged 30 points right
- **THEN** column C is 30 points wider and B2 is still the selection

### Requirement: The row, column and region chords

At rest the spreadsheet SHALL answer Excel's selection chords on every target: Shift+Space SHALL select
the whole rows of the active range, Ctrl+Space (⌃Space on a Mac) its whole columns, and ⌘A (Ctrl+A)
SHALL select the current region, the block of cells in use joined to the active cell through cells in
use, its rows, columns and diagonals, then, pressed again or with nothing around the active cell in use,
the whole sheet. Shift+Space SHALL type nothing.

#### Scenario: Shift+Space

- **WHEN** B3:C4 is selected and Shift+Space is pressed
- **THEN** the rows 3 and 4 are selected, whole, and no cell is being edited

#### Scenario: Ctrl+Space on a Mac

- **WHEN** B3 is selected on a Mac's keyboard and ⌃Space is pressed
- **THEN** the column B is selected, whole

#### Scenario: The current region, then the sheet

- **WHEN** B2:D5 hold values, the rest of the sheet is empty, C3 is the active cell and ⌘A is pressed
  twice
- **THEN** the first press selects B2:D5 and the second the whole sheet

#### Scenario: An empty cell alone

- **WHEN** nothing around H9 is in use, H9 is the active cell and ⌘A is pressed
- **THEN** the whole sheet is selected

### Requirement: The page and corner chords

At rest the spreadsheet SHALL move by a screen of columns with Alt+PageDown and Alt+PageUp, keeping the
active cell's row, and stretch by it with Shift; Ctrl+Backspace (⌃Delete on a Mac) SHALL bring the
active cell into view without moving it or the selection; and Ctrl+. (⌃. on a Mac) SHALL move the
active cell to the active range's next corner, clockwise from the top-left, with the range unchanged, on
every target.

#### Scenario: A screen of columns

- **WHEN** A1 is the active cell in a sheet whose window shows eight whole columns, and Alt+PageDown is
  pressed
- **THEN** I1 is the active cell and lies inside the window

#### Scenario: Bring the active cell back

- **WHEN** B2 is the active cell, the grid is scrolled until B2 is out of view, and Ctrl+Backspace is
  pressed
- **THEN** B2 lies inside the window, and the selection is still B2

#### Scenario: The next corner

- **WHEN** B2:D5 is selected with B2 active and Ctrl+. is pressed three times
- **THEN** the active cell is D2, then D5, then B5, and the selection is still B2:D5

### Requirement: Ctrl+End reaches the last cell in use

⌘End (Ctrl+End) SHALL make the active cell the cell at the last row and the last column that hold a
value, and ⌘⇧End (Ctrl+Shift+End) SHALL stretch the active range to it, on every target; in a sheet
that holds nothing it SHALL be the first cell.

#### Scenario: The last cell in use

- **WHEN** a sheet of 1000 rows by 26 columns holds values in B3 and E2 alone, and Ctrl+End is pressed
  from A1
- **THEN** E3 is the active cell

#### Scenario: Stretched to it

- **WHEN** the same sheet has B2 active and Ctrl+Shift+End is pressed
- **THEN** the selection runs from B2 to E3

### Requirement: Ctrl+Y redoes

At rest ⌘Y (Ctrl+Y) SHALL redo, as ⌘⇧Z (Ctrl+Shift+Z) does, on every target.

#### Scenario: Redo with Ctrl+Y

- **WHEN** B2 is set to `7`, the edit is undone, and Ctrl+Y is pressed
- **THEN** B2 holds `7`

### Requirement: Enter after a run of Tabs returns to its column

When Tab, from a single selected cell, steps the active cell to the right one or more times, and Enter
follows with no other move in between, Enter SHALL move the active cell down to the next row in the
column the run of Tabs began in, at rest and when it commits an entry, on every target.

#### Scenario: A row filled in with Tab

- **WHEN** B2 is the active cell, `1` Tab `2` Tab `3` Enter are typed
- **THEN** B2, C2 and D2 hold `1`, `2` and `3`, and B3 is the active cell

#### Scenario: A move breaks the run

- **WHEN** B2 is the active cell, Tab is pressed, then ↓, then Enter
- **THEN** C4 is the active cell

### Requirement: The cell cursor glides

When a key or a press moves the active cell, its outline SHALL glide from where it stood to where it
lands, over the duration and the curve of the theme's motion tokens, on every target; under the
platform's reduced-motion setting it SHALL jump.

#### Scenario: A glide

- **WHEN** B2 is the active cell and → is pressed
- **THEN** halfway through the theme's base motion the outline stands between B2 and C2, and at its end
  around C2, on the web as on Photon

#### Scenario: Reduced motion

- **WHEN** the platform asks for reduced motion and → is pressed from B2
- **THEN** the outline stands around C2 on the first frame

### Requirement: The status bar sums what is selected

A status bar the app places beside the grid over the same controller SHALL show, while the selection
holds two or more cells in use, the Count of the cells in use it covers and, when one or more of them
hold a number, their Sum and their Average, each counted once however many ranges cover it, written in
the page's language and culture. Until values are typed, a cell SHALL count as a number when its text
reads as a plain number in the page's culture: a sign, digits, the culture's decimal separator and an
exponent, no thousands separator. In add mode the status bar SHALL say so.

#### Scenario: Sums in Portuguese

- **WHEN** the page's culture is `pt-BR`, B2 to B4 hold `7,01`, `14,02` and `texto`, and B2:B4 is
  selected
- **THEN** the status bar shows `Média: 10,515`, `Contagem: 3` and `Soma: 21,03`, on the web as on Photon

#### Scenario: One cell

- **WHEN** B2 holds `7` and B2 is selected
- **THEN** the status bar shows nothing

#### Scenario: Ranges that overlap

- **WHEN** B2 and B3 hold `1` and `2`, and B2:B3 and B3:B4 are selected
- **THEN** the status bar shows `Sum: 3`, `Average: 1.5` and `Count: 2`

## MODIFIED Requirements

### Requirement: The selection is drawn over the cells

Every selected range SHALL be drawn above the cells as one rectangle: a translucent fill under one
outline, with the active cell left unfilled inside the active range. The active cell's ring, the fill's
preview and the fill handle SHALL be drawn above the cells too, and a cell SHALL be drawn from its value
alone, on every target. A column or row header whose band a range covers SHALL be emphasized, and
strongly when a range covers its whole column or row. While the grid does not hold the keyboard, the
ranges and the ring SHALL be drawn in the dimmed colours. Where any of them crosses a pinned header it
SHALL be cut there as the viewport's marks are.

#### Scenario: A range selected

- **WHEN** B2:D4 is selected with B2 active
- **THEN** the cells B2 to D4 are drawn as every other cell is, one outline is drawn around B2:D4, the
  fill covers every cell of it but B2, and the active ring is drawn around B2

#### Scenario: A whole column

- **WHEN** the column C is selected, whole
- **THEN** the header of C is drawn in the strong emphasis, and the headers of the rows in the
  band's emphasis

#### Scenario: The keyboard elsewhere

- **WHEN** B2:D4 is selected and the keyboard moves to a button beside the grid
- **THEN** the range's outline and fill are drawn in the dimmed colours, on the web as on Photon

### Requirement: The sheet's colours come from its theme slot

The spreadsheet SHALL take its gridlines, its headers' colours, the emphasized and the strongly
emphasized header, its cells' colours, the range's fill and outline, the active outline, the dimmed
selection, the fill's preview and the handle from the app theme's sheet slot where the slot names them,
and from the theme's foundation colours where it does not, on every target.

#### Scenario: A theme that names a gridline

- **WHEN** an app's theme names the sheet's gridline colour
- **THEN** the gridlines are drawn in it, on the web as on Photon

#### Scenario: A theme that names nothing for sheets

- **WHEN** an app's theme answers no sheet slot
- **THEN** the gridlines are drawn in the theme's border colour and the headers in its subtle surface

#### Scenario: A theme that names the range's outline

- **WHEN** an app's theme names the sheet's range outline
- **THEN** every selected range is outlined in it, on the web as on Photon
