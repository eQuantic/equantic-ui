## MODIFIED Requirements

### Requirement: Undo and redo raise the edit they made

`CodeEditorController.Changed` SHALL carry the edits an undo or a redo made, in one `CodeChange`: the range
each replaced, the text it removed and the text it wrote. A change with no edits, `Replaced`, SHALL mean
only a document replaced whole.

#### Scenario: Undoing a word

- **WHEN** `ab` is typed into an empty editor and undone
- **THEN** `Changed` carries one change whose one edit removed `ab` from 0:0 to 0:2 and wrote nothing
