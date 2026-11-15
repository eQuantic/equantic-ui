# Spec Delta

## MODIFIED Requirements

### Requirement: Several ranges are selected at once

The spreadsheet SHALL hold a selection of one or more ranges, the last of which is the active range and
holds the active cell, and SHALL draw every one of them, on every target. A press with ⌘ (Ctrl on
Windows and Linux) SHALL add the cell under it as a new range and make it active, and dragging that
press SHALL stretch the new range; Shift+F8 SHALL enter add mode, in which a press or a drag adds a range
as it would with ⌘ or Ctrl, until Shift+F8 is pressed again or Escape is. A press without a modifier, an
arrow, Go To and every other move that selects SHALL leave one range. Copy, cut, paste, the fill down
and right chords and the fill handle SHALL act on the active range alone; Delete SHALL clear every range,
and Ctrl+Enter (⌘↩) SHALL fill every range with the entry, as one step undo takes back. An undo and a
redo of any step SHALL bring back the selection as it stood on that side of it, every range and the
active cell where it was, and a paste SHALL leave the active cell where it landed.

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

- **WHEN** B2 and D4:D5 hold values, B2 and D4:D5 are selected with D4 active, and Delete is pressed
- **THEN** B2, D4 and D5 are empty, one undo restores all three with B2 and D4:D5 selected and D4
  active, and a redo empties them again with the same selection

#### Scenario: Copy takes the active range

- **WHEN** B2 holds `1`, D4 holds `2`, B2 and D4 are selected, and the selection is copied
- **THEN** the clipboard holds `2`

#### Scenario: Ctrl+Enter fills every range

- **WHEN** B2 and D4:D5 are selected, `x` is typed and Ctrl+Enter (⌘↩) is pressed
- **THEN** B2, D4 and D5 hold `x`, and one undo empties all three with B2 and D4:D5 selected

#### Scenario: An undo keeps the active cell where it was

- **WHEN** B2:C3 is selected, Tab moves the active cell to C2, Delete is pressed, E5 is selected and the
  step is undone
- **THEN** B2:C3 is selected with C2 active
