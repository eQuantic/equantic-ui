# spreadsheet Specification

## Purpose
How the shared spreadsheet lays out its grid, its headers and its marks, and answers a press, a key
and the clipboard, the same on the web and on Photon, through an engine of its own.

## Requirements

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
with its value in Edit mode with the caret where the press landed, a press within 8 points of the
selection's bottom-right corner SHALL begin a fill, a plain press that is dragged SHALL move the
selection's far corner with the pointer, the cell it began on staying active, and the release of a fill SHALL land it as one step that undo
takes back. While a cell is being edited, a press inside the editor's bounds SHALL place the entry's
caret there and switch the entry to Edit mode, a drag from it SHALL select the entry's text, and a
double click on a word SHALL select that word; a press outside the editor SHALL commit the edit first
and then act as it would on a sheet at rest. A finger's press SHALL select the cell it began on when it
lifts, and SHALL select nothing when the grid takes it as a pan. A press the system cancels SHALL land
nothing: a fill pours nothing, and a drag stops where it stood.

#### Scenario: A press in a scrolled window

- **WHEN** a sheet scrolled down by 40 rows and across by one column is pressed in the second column
  and the third row it shows under its headers
- **THEN** the selection is that one cell, row 42 counted from 0, column C, on the web as on Photon

#### Scenario: The first press on a sheet taller than its view

- **WHEN** a page whose sheet is taller than the scroller around it takes its first press, on B3
- **THEN** B3 is selected, and the scroller has not moved

#### Scenario: A drag from a plain press

- **WHEN** a press on B2 is dragged to D4 and released
- **THEN** the selection runs from B2 to D4 and the active cell is B2, where the drag began, as in Excel
  and Sheets

#### Scenario: A fill the system takes away

- **WHEN** B2 holds `7`, its fill handle is dragged to B4, and the system cancels the press before it
  lifts
- **THEN** B3 and B4 stay empty, and undo has nothing to take back

#### Scenario: A press with Shift

- **WHEN** B2 is selected and a press with Shift lands on D5
- **THEN** the selection runs from B2 to D5, and B2 is still the active cell

#### Scenario: A double click

- **WHEN** the second press of a double click lands on a cell holding `4200`, between its `2` and its
  first `0`
- **THEN** that cell is being edited in Edit mode, its entry is `4200`, and the caret stands after the
  `2`, on the web as on Photon

#### Scenario: A press inside the editor

- **WHEN** B2 is being edited with the entry `hello world` and the caret at its end, and a press lands
  in the editor between `hello` and its space
- **THEN** B2 is still being edited, in Edit mode, its entry is still `hello world`, and the caret
  stands after `hello`

#### Scenario: A press outside the editor

- **WHEN** B2 is being edited with the entry `7` and a press lands on D5
- **THEN** B2 holds `7` and D5 is selected

#### Scenario: The fill handle

- **WHEN** B2 holds `7`, B2 is selected, and a press on its bottom-right corner is dragged to B4 and
  released
- **THEN** B3 and B4 hold `7`, and one undo empties both

#### Scenario: The fill handle's pointer

- **WHEN** B2 is selected and the pointer rests on its bottom-right corner
- **THEN** the pointer is a crosshair, on the web as on Photon, and over the middle of B2 it is not

#### Scenario: The pointer over the editor

- **WHEN** B2 is being edited and the pointer rests inside the editor
- **THEN** the pointer is a text beam, on the web as on Photon

#### Scenario: A finger's tap

- **WHEN** B2 is selected and a finger lands on D5 and lifts without moving
- **THEN** the selection is D5

#### Scenario: A finger's pan

- **WHEN** B2 is selected and a finger lands on D5 and drags upward by 200 points before it lifts
- **THEN** the grid has scrolled down and the selection is still B2, on the web as on Photon

### Requirement: The clipboard keys follow the host's clipboard

A host that hands the spreadsheet a clipboard SHALL have the copy, cut and paste chords (⌘C, ⌘X and
⌘V on Apple's keyboards, Ctrl elsewhere) claimed and served through it. At rest they SHALL carry the
selected cells as tab-separated values; while a cell is being edited they SHALL carry the entry's
selected text, and a paste SHALL insert the clipboard's text at the caret as one step of the entry's
undo. A host that hands none SHALL find those chords unclaimed, so the platform's own copy, cut and
paste events carry the same text. A cut SHALL clear the selection only when its values reached a
clipboard.

#### Scenario: A copy on Photon

- **WHEN** B2:C3 holds `1`, `2`, `3` and `4` row by row, and ⌘C is pressed over it on Photon with a
  clipboard
- **THEN** the clipboard holds `1\t2\n3\t4`

#### Scenario: A copy in the browser

- **WHEN** ⌘C is pressed over a focused sheet in the browser
- **THEN** the key is not claimed, and the copy event that follows carries the selection's
  tab-separated values

#### Scenario: A cut with nowhere to go

- **WHEN** ⌘X is pressed over B2 holding `7` on a host that handed no clipboard
- **THEN** B2 still holds `7`

#### Scenario: A copy while editing

- **WHEN** B2 is being edited with the entry `hello world` and `world` selected, and ⌘C is pressed,
  on Photon with a clipboard and in the browser through its copy event
- **THEN** the clipboard holds `world`, and B2 is still being edited

#### Scenario: A paste while editing

- **WHEN** B2 is being edited with the entry `ab` and the caret between `a` and `b`, and the
  clipboard's text `x\ty` is pasted
- **THEN** the entry is `ax\tyb`, no other cell changed, and one undo while editing gives back `ab`

### Requirement: The spreadsheet's engine is an assembly the SDK adds

An app built with either SDK SHALL reach the spreadsheet's engine through the `eQuantic.UI.Sheet`
namespace with no package reference of its own, and the vocabulary assembly SHALL declare none of the
engine's types.

#### Scenario: A page that builds a sheet

- **WHEN** a page with `using eQuantic.UI.Sheet;` keeps `new SheetController(rows: 200, cols: 8)` and
  shows it in a `Spreadsheet`, in a project whose file names only the SDK
- **THEN** it builds, and the sheet takes clicks and keys in the browser

#### Scenario: The vocabulary's own types

- **WHEN** the types of `eQuantic.UI.Primitives` are listed
- **THEN** none of them is the spreadsheet's controller, document, history, keymap or cell types

### Requirement: A cell's address holds at Excel's extent

A cell SHALL be addressable at every row up to 1 048 576 and every column up to 16 384, and the number
the engine keys a cell by SHALL be the same in .NET and in the browser for every such address.

#### Scenario: The last cell of Excel's grid

- **WHEN** the cell at row 1 048 576 and column 16 384 is written `x` and read back, in .NET and in the
  browser
- **THEN** both read `x`, its address reads `XFD1048576`, and its key is 17 179 869 183 on both

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

### Requirement: Text reaches a cell through the platform's text door

The spreadsheet SHALL take the text a person types from the platform's own text input on both targets,
never from the names of the keys pressed: a dead key's result, an input method's composition and its
commit, an emoji from the system's picker and a character typed with AltGr SHALL reach the active cell.
A composition SHALL show in the cell's editor while it is built, marked as a composition, and SHALL
enter the entry as one edit when it commits; a composition cancelled SHALL leave the entry as it was
before it began. A composition or a character typed while no cell is being edited SHALL begin an entry
in Enter mode that replaces the active cell's value, as typing does. The platform's input method SHALL
open its window at the caret while a cell is being edited, and at the active cell before.

#### Scenario: A dead key in the browser

- **WHEN** a focused sheet in Chromium with B2 active receives the composition of `´` and then `a`,
  committed as `á`, then `ã` and `ç` the same way
- **THEN** B2 is being edited and its entry is `áãç`

#### Scenario: A composed Japanese word

- **WHEN** an input method composes `にほんご` over B2 and commits `日本語`, in Chromium and on Photon
  through the macOS shell's text input client
- **THEN** while it composes, B2's editor shows `にほんご` marked; after the commit the entry is
  `日本語`, and one undo while editing empties it

#### Scenario: A cancelled composition

- **WHEN** B2 is being edited with the entry `ab`, a composition of `x` begins after `b` and is
  cancelled
- **THEN** the entry is `ab` and the caret stands after `b`

#### Scenario: An emoji

- **WHEN** the system's emoji picker inserts `👍🏽` into a focused sheet with B2 active, in Chromium and
  on Photon
- **THEN** B2's entry is `👍🏽`, one Backspace empties it, and a commit leaves B2 holding `👍🏽`

### Requirement: The entry is a text document

A cell being edited SHALL hold its entry as a text document with a caret and a selection. The caret
SHALL move by one character, by one word in the keyboard's own tradition (⌥ with the arrows on Apple's
keyboards, Ctrl elsewhere), and to the line's start and end with Home and End; Shift SHALL extend the
selection with any of those moves; typing SHALL replace the selection; Backspace and Delete SHALL
remove the selection or one character, never half of a text element. Undo and redo while editing SHALL
step through the entry's own edits and SHALL never reach the sheet's history. Committing SHALL be one
step of the sheet's undo, and cancelling SHALL leave the cell as it was and the sheet's undo untouched.

#### Scenario: F2 and the arrows

- **WHEN** B2 holds `hello`, F2 is pressed, then ← twice, then `X` is typed
- **THEN** B2's entry is `helXlo`, and B2 still holds `hello` until the entry is committed

#### Scenario: Home, End and a word

- **WHEN** B2 is being edited in Edit mode with the entry `one two three` and the caret at its end,
  and the move to the line's start (Home, ⌘← on a Mac), then the word move to the right with Shift
  (Ctrl+Shift+→, ⌥⇧→ on a Mac), are pressed
- **THEN** the selection is `one `, the word and the space after it, in both traditions: the code
  engine's word move

#### Scenario: Undo while editing

- **WHEN** B2 holds `x`, F2 is pressed, `yz` is typed, and undo is pressed once
- **THEN** the entry is `x` again, B2 is still being edited, and the sheet's own undo still has
  nothing of this edit to take back

#### Scenario: A commit is one step

- **WHEN** B2 holds `x`, F2 is pressed, `yz` is typed and Enter is pressed, then the sheet's undo is
  pressed once
- **THEN** B2 holds `x`

### Requirement: An entry is in Enter mode or Edit mode

An entry begun by typing into a cell, by a composition, by Backspace or by a date or time chord SHALL
be in Enter mode, where ←, →, ↑ and ↓, with or without Shift, SHALL commit the entry and move the active
cell (Shift stretching the selection). An entry begun with F2, with a double click or with a press in
the formula bar SHALL be in Edit mode, where those keys SHALL move the entry's caret. F2 while editing
SHALL switch between the two modes, and a press inside the editor SHALL switch to Edit mode. The mode
SHALL be readable by the app, so a status line can show it.

#### Scenario: An arrow in Enter mode

- **WHEN** B2 is active, `7` is typed and → is pressed
- **THEN** B2 holds `7` and C2 is the active cell

#### Scenario: An arrow in Edit mode

- **WHEN** B2 holds `78`, F2 is pressed and ← is pressed
- **THEN** B2 is still being edited and the caret stands between `7` and `8`

#### Scenario: F2 toggles

- **WHEN** B2 is active, `7` is typed, F2 is pressed and ← is pressed
- **THEN** B2 is still being edited in Edit mode, with the caret before `7`

### Requirement: The keys of an entry

While a cell is being edited, Enter SHALL commit the entry and step down (up with Shift), Tab SHALL
commit it and step right (left with Shift), each walking inside a selection of several cells as at
rest, and Escape SHALL cancel it. Alt+Enter (⌥↩) SHALL break the line inside the entry. ⌘↩ on Apple's
keyboards and Ctrl+Enter elsewhere SHALL write the entry into every cell of the selection as one step of
the sheet's undo and leave the selection where it was. The date chord (Ctrl+; or ⌃; on a Mac) SHALL
insert today's date and the time chord (Ctrl+Shift+; or ⌘; on a Mac) the time, written by the page's
culture's short date and short time patterns; pressed at rest, either SHALL begin an entry in Enter mode
holding that text.

#### Scenario: A line break

- **WHEN** B2 is active, `one` is typed, Alt+Enter is pressed, `two` is typed and Enter is pressed
- **THEN** B2 holds `one\ntwo`, and a copy of B2 carries it quoted as Excel writes it

#### Scenario: Fill the selection with the entry

- **WHEN** B2:C4 is selected with B2 active, `x` is typed and Ctrl+Enter is pressed (⌘↩ on a Mac)
- **THEN** all six cells hold `x`, B2:C4 is still selected with B2 active, and one undo empties all six

#### Scenario: The date and the time in the page's culture

- **WHEN** the date chord is pressed over an empty B2 on 2026-10-25 at 15:45, a space is typed and the
  time chord is pressed, once in a page whose culture is `en-US` and once in `pt-BR`
- **THEN** B2's entry is `10/25/2026 3:45 PM` in `en-US` and `25/10/2026 15:45` in `pt-BR`, on the web
  as on Photon

#### Scenario: Escape cancels

- **WHEN** B2 holds `x`, `yz` is typed and Escape is pressed
- **THEN** B2 holds `x`, nothing is being edited, and the sheet's undo has nothing to take back

### Requirement: Backspace clears the active cell and edits it

At rest, Backspace SHALL begin an empty entry over the active cell in Enter mode, the way Excel does,
so the value comes back with Escape and goes with a commit; it SHALL leave the other cells of a
selection as they are. Delete SHALL clear every cell of the selection as one undo step without editing.

#### Scenario: Backspace then a value

- **WHEN** B2 holds `old`, B2:C3 is selected with B2 active, Backspace is pressed, `new` is typed and
  Enter is pressed
- **THEN** B2 holds `new` and C2, B3 and C3 are as they were

#### Scenario: Backspace then Escape

- **WHEN** B2 holds `old`, Backspace is pressed and then Escape
- **THEN** B2 holds `old`

#### Scenario: Delete

- **WHEN** B2 and C2 hold `1` and `2`, B2:C2 is selected and Delete is pressed
- **THEN** both are empty, nothing is being edited, and one undo gives both back

### Requirement: The editor grows over its neighbours

The cell's editor SHALL draw the entry in the cell's own type, colour and alignment, over the cell,
and SHALL grow as the text grows: to the right over the next columns, in whole columns, while the
entry's widest line needs more room, and down over the next rows, in whole rows, while its lines need
more height, never past the last column or the last row in view. When the widest line needs more than
that, the editor's text SHALL slide so the caret stays inside it. The editor's background SHALL hide
the cells it covers, and the grid SHALL keep them as they are.

#### Scenario: Wider than its cell

- **WHEN** B2 in a grid of 100-point columns is edited and a line 230 points wide in the cell's type
  is typed
- **THEN** the editor covers B2, C2 and D2, and C2 and D2 hold what they held

#### Scenario: Two lines in a short row

- **WHEN** B2 in a row as tall as one line is edited with the entry `one\ntwo`
- **THEN** the editor covers B2 and B3, and both lines show

#### Scenario: Wider than the view

- **WHEN** the entry's only line is wider than every column from the active cell to the right edge of
  the view, and the caret is at its end
- **THEN** the editor reaches the right edge of the view, and the caret is drawn inside it

### Requirement: A cell shows the lines of its value

A cell whose value holds a line break SHALL draw its lines from its top, as many as its row's height
shows.

#### Scenario: A value of two lines

- **WHEN** B2 holds `one\ntwo` in a row tall enough for two lines
- **THEN** B2 shows `one` above `two`

### Requirement: The formula bar is a second view of the entry

The spreadsheet family SHALL offer a formula bar the app places beside the grid, over the same
controller. At rest it SHALL show the active cell's value. While a cell is being edited it SHALL show
the entry, the same text the cell's editor shows after every keystroke, composition and undo, because
both are views of one document. A press in the bar SHALL begin editing the active cell in Edit mode
with the caret where it landed, or move the caret there if a cell is being edited, and the caret SHALL
then be drawn in the bar and not in the cell. Typing in the bar SHALL edit the same entry, Enter, Tab
and Escape SHALL do there what they do in the cell, and after any of them the keyboard SHALL return to
the grid.

#### Scenario: Keystroke by keystroke

- **WHEN** a sheet and its formula bar are drawn, B2 is active and `a`, `b`, `c` are typed into the
  grid one by one
- **THEN** after each keystroke the bar and B2's editor show the same text, `a`, `ab`, `abc`, in
  Chromium and on Photon

#### Scenario: Editing from the bar

- **WHEN** B2 holds `10`, the formula bar is pressed after its last character, `0` is typed and Enter
  is pressed
- **THEN** B2 holds `100`, B3 is active, and the grid has the keyboard

#### Scenario: Escape in the bar

- **WHEN** B2 holds `10`, the formula bar is pressed, `5` is typed and Escape is pressed
- **THEN** B2 holds `10`, nothing is being edited, and the grid has the keyboard

### Requirement: The name box shows where the selection is and goes where it is told

The spreadsheet family SHALL offer a name box the app places beside the grid, over the same
controller. At rest it SHALL show the active cell's address; while a drag draws a range of more than
one cell it SHALL show the range's size as rows by columns, in the app's language. A press in it SHALL
select its text for typing; Enter SHALL go to what was typed, a cell (`B7`, `$B$7`), a range (`B7:D9`),
whole rows (`3:5`) or whole columns (`B:D`), in either case, selecting it, revealing its first cell and
returning the keyboard to the grid; text that names nothing in the grid SHALL leave the selection as
it was and mark the name box as invalid with its text kept; Escape SHALL restore the address and return
the keyboard to the grid. The Go To chord (Ctrl+G, ⌃G on a Mac) at rest SHALL give the keyboard to
the name box when one is drawn, and SHALL be left unclaimed when none is; F5 SHALL do the same on
Photon, and in a browser SHALL stay the browser's own key, which reloads the page, as Excel for the web
leaves it by default.

#### Scenario: The address and the size of a drag

- **WHEN** a press on B2 is dragged to D6 and held
- **THEN** the name box shows `5R x 3C`, and after the release it shows `B2`, the active cell, where the
  drag began

#### Scenario: Go to a cell far away

- **WHEN** `xfd1048576` is typed into the name box and Enter is pressed
- **THEN** XFD1048576 is selected and in view, and the grid has the keyboard

#### Scenario: Go to columns

- **WHEN** `B:D` is typed into the name box and Enter is pressed
- **THEN** columns B to D are selected, with B1 active

#### Scenario: A name that names nothing

- **WHEN** `B0` is typed into the name box and Enter is pressed
- **THEN** the selection is as it was, the name box is marked invalid and still holds `B0`

#### Scenario: Go To with a name box

- **WHEN** a sheet and its name box are drawn, the grid has the keyboard and the Go To chord is
  pressed, in Chromium and on Photon, and then F5 on Photon
- **THEN** each time the name box has the keyboard with its text selected

#### Scenario: F5 in a browser

- **WHEN** a sheet and its name box are drawn in Chromium, the grid has the keyboard and F5 is pressed
- **THEN** the sheet does not claim the key, and the browser's own default for it is not prevented

### Requirement: Escape releases Tab

At rest, Escape SHALL release the sheet's hold on Tab, so that the next Tab or Shift+Tab is left to
whatever is around the sheet and the keyboard moves to the next or the previous control; any other key
that is not a modifier SHALL take the hold again. While a cell is being edited, Escape SHALL cancel the
edit and keep the hold. This SHALL hold on both targets, inside a dialog and outside one.

#### Scenario: Leaving a sheet in a page

- **WHEN** a page holds a sheet and then a button, the sheet has the keyboard, and Escape then Tab are
  pressed
- **THEN** the button has the keyboard and the active cell has not moved, in Chromium and on Photon

#### Scenario: Leaving a sheet in a dialog

- **WHEN** a sheet stands between two buttons of a dialog that does not close on Escape, the sheet has
  the keyboard, and Escape then Tab are pressed; and again with Escape then Shift+Tab
- **THEN** the button after the sheet has the keyboard, and then the one before it, and the active cell
  has not moved, in Chromium and on Photon

#### Scenario: A sheet at the end of a dialog in a browser

- **WHEN** a sheet is the last control of a dialog in Chromium, the sheet has the keyboard, and Escape
  then Tab are pressed
- **THEN** the focus moves on as the dialog's trap cycles, to its first control

#### Scenario: Another key takes Tab back

- **WHEN** the sheet has the keyboard and Escape, → and then Tab are pressed
- **THEN** the sheet still has the keyboard and the active cell is two columns to the right

#### Scenario: Escape while editing

- **WHEN** B2 is being edited and Escape then Tab are pressed
- **THEN** the edit is cancelled, the sheet still has the keyboard, and C2 is active

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
- **THEN** the selection is B2 and D4:E6, and D4 is the active cell

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
the page's language and culture, the Sum and the Average as Excel's General format writes them in its
status bar: in eleven characters, the places that fit after the whole part, and in scientific notation
past them or where it keeps more of the value; no Sum and no Average where the sum passes what a decimal holds. The summary SHALL read the
cells the ranges cover or the cells in use, whichever are fewer. Until values are typed, a cell SHALL count as a number when its text
reads as a plain number in the page's culture: a sign, digits, the culture's decimal separator and an
exponent, no thousands separator. In add mode the status bar SHALL say so.

#### Scenario: Sums in Portuguese

- **WHEN** the page's culture is `pt-BR`, B2 to B4 hold `7,01`, `14,02` and `texto`, and B2:B4 is
  selected
- **THEN** the status bar shows `Média: 10,515`, `Contagem: 3` and `Soma: 21,03`, on the web as on Photon

#### Scenario: One cell

- **WHEN** B2 holds `7` and B2 is selected
- **THEN** the status bar shows nothing

#### Scenario: A sum of eleven digits

- **WHEN** B2 and B3 hold `123456789,12` and `12345678901` in pt-BR, and B2:B3 is selected
- **THEN** the status bar shows `Soma: 12469135690` and `Média: 6234567845`

#### Scenario: A sum too small for the places

- **WHEN** B2 and B3 hold `0.0000000001` in en-US, and B2:B3 is selected
- **THEN** the status bar shows `Sum: 2E-10` and `Average: 1E-10`, where the places wrote `0`

#### Scenario: An average that does not end

- **WHEN** B2 to B4 hold `1`, `2` and `2`, and B2:B4 is selected
- **THEN** the status bar shows `Average: 1.666666667`, where a decimal's own text has 28 digits

#### Scenario: Ranges that overlap

- **WHEN** B2 and B3 hold `1` and `2`, and B2:B3 and B3:B4 are selected
- **THEN** the status bar shows `Sum: 3`, `Average: 1.5` and `Count: 2`

### Requirement: The active cell stays where the selection began

A drag, a press with Shift, and Shift with an arrow, a data-edge jump, Home, End, Ctrl+End or a page key
SHALL stretch the active range from the end it last stretched, its anchor and the active cell kept where
the range began, as Excel and Sheets keep them; a plain arrow or page key SHALL then move from the active
cell. Setting the selection from code SHALL make its anchor the active cell. An undo or a redo SHALL leave
the active cell at the anchor of the selection it restores.

#### Scenario: Shift and an arrow

- **WHEN** B2 is selected and Shift+↓ is pressed twice
- **THEN** the selection runs from B2 to B4, B2 is the active cell, and the name box shows `B2`, on the web
  as on Photon

#### Scenario: A plain arrow after a stretch

- **WHEN** B2:B4 was stretched from B2 with Shift+↓ and ↓ is pressed
- **THEN** B3 alone is selected and active

#### Scenario: Shift and a page key twice

- **WHEN** A21 is selected and Shift+PageDown is pressed twice in a grid that shows 20 rows
- **THEN** the selection runs from A21 to A61 and A21 is the active cell

### Requirement: The grid is read aloud

The spreadsheet's grid SHALL read as a grid named after the sheet, on every target: the column letters
as the headers of their columns, the row numbers as the headers of their rows, the corner as the empty
header of the row numbers' column, and each cell as a grid cell whose name is what it shows, its row and its column those
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
