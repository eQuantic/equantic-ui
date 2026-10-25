# Spec Delta

## MODIFIED Requirements

### Requirement: A press on a sheet means the same on every target

The spreadsheet SHALL decide what a pointer means in its engine, from the point in the surface's own
coordinates and the grid's offset at that moment, so the web and Photon answer the same press the same
way. A press of a mouse or a stylus SHALL select the cell under it, a press with Shift SHALL stretch the
selection from its anchor to that cell, the second press of a double click SHALL begin editing the cell
with its value in Edit mode with the caret where the press landed, a press within 8 points of the
selection's bottom-right corner SHALL begin a fill, a plain press that is dragged SHALL move the
selection's far corner with the pointer, and the release of a fill SHALL land it as one step that undo
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
- **THEN** the selection runs from B2 to D4 and the active cell is D4

#### Scenario: A fill the system takes away

- **WHEN** B2 holds `7`, its fill handle is dragged to B4, and the system cancels the press before it
  lifts
- **THEN** B3 and B4 stay empty, and undo has nothing to take back

#### Scenario: A press with Shift

- **WHEN** B2 is selected and a press with Shift lands on D5
- **THEN** the selection runs from B2 to D5

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

## ADDED Requirements

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
  and Home, then the word move to the right with Shift, are pressed
- **THEN** the selection is `one` on Apple's keyboards and `one ` elsewhere, as each platform's own
  text fields select a word

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
- **THEN** the name box shows `5R x 3C`, and after the release it shows `B2`

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
