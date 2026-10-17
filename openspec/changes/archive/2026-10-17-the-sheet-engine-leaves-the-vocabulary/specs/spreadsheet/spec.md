## ADDED Requirements

### Requirement: A press on a sheet means the same on every target

The spreadsheet SHALL decide what a pointer means in its engine, from the point in the surface's own
coordinates and the window the surface draws, so the web and Photon answer the same press the same
way. A press SHALL select the cell under it, a press with Shift SHALL stretch the selection from its
anchor to that cell, the second press of a double click SHALL begin editing the cell with its value,
a press within 8 points of the selection's bottom-right corner SHALL begin a fill, a plain press that
is dragged SHALL move the selection's far corner with the pointer, and the release of a fill SHALL
land it as one step that undo takes back. A press that lands while a cell is being edited SHALL
commit the edit first. A press the system cancels SHALL land nothing: a fill pours nothing, and a drag
stops where it stood.

#### Scenario: A press in a scrolled window

- **WHEN** a sheet whose window starts at row 40 is pressed in the second column of the third row it
  draws
- **THEN** the selection is that one cell, row 42 counted from 0, column B, on the web as on Photon

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

### Requirement: The clipboard keys follow the host's clipboard

A host that hands the spreadsheet a clipboard SHALL have the copy, cut and paste chords (⌘C, ⌘X and
⌘V on Apple's keyboards, Ctrl elsewhere) claimed and served through it, as tab-separated values. A
host that hands none SHALL find those chords unclaimed, so the platform's own copy, cut and paste
events carry the same text. A cut SHALL clear the selection only when its values reached a
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
