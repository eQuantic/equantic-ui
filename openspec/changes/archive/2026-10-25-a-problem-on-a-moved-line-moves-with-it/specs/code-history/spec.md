## ADDED Requirements

### Requirement: An undo step holds the edits a command made

An undo step SHALL hold every edit one command made, in the order it made them. Undo SHALL take them back
last first and redo SHALL make them again first first, and the editor SHALL raise `Changed` for each
replacement it makes, through an edit, an undo and a redo alike, each in the coordinates of the document it
was made on. The selection a step leaves SHALL be its last edit's, and undo SHALL put back the one its first
edit found. A step of several edits SHALL never join a run of typing.

#### Scenario: A move is one step of two edits

- **WHEN** the second of three lines is moved up with ⌥↑
- **THEN** `Changed` is raised twice, one undo puts the three lines back as they were with the caret where
  it stood, and one redo moves the line again

#### Scenario: A listener follows every replacement

- **WHEN** a problem is reported on the second of three lines, the line is moved up, then undone and redone
- **THEN** the problem stands on the moved line's text after the move, after the undo and after the redo

#### Scenario: Typing still joins a run

- **WHEN** "abc" is typed, a character at a time, then undone
- **THEN** one undo takes back the three characters
