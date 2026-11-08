## ADDED Requirements

### Requirement: An undo over several ranges gives every range back

An undoable step SHALL record every range of the selection, the active one last, and the active cell, from before
it and from after it, and an undo and a redo SHALL restore all of them, on every target.

#### Scenario: Delete over two ranges, undone and redone

- **WHEN** B2 and D4:D5 hold `x`, B2 and D4:D5 are selected with D5 the active cell, Delete is pressed, and the
  step is undone and then redone
- **THEN** after the undo B2, D4 and D5 hold `x`, the selection is B2 and D4:D5 and D5 is the active cell, and
  after the redo the selection and the active cell are what they were after the Delete

### Requirement: A reader's activation reads the cell's place

A reader's activation of a cell or a header SHALL be answered by the place's row and column in the table the
sheet is drawn in, never by where the place is drawn, on every target.

#### Scenario: A row half under the column headers

- **WHEN** the sheet is scrolled so that the first row's centre lies under the column headers, and a reader
  activates B1
- **THEN** B1 is the active cell and the selection is B1, not column B
