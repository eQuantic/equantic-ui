## ADDED Requirements

### Requirement: An undo step holds the edits a command made

An undo step SHALL hold every edit one command made, in the order it made them. Undo SHALL take them back
last first and redo SHALL make them again first first. The editor SHALL raise `Changed` once for each
change, an edit, a step, an undo and a redo alike, once all of its replacements are made, with all of them
in the order made, each range in the coordinates the replacements before it left, and each with the
selection the change found and the one it left. An edit a listener makes while it hears a change SHALL be
made on the text the change left, as a step of its own, and every listener SHALL hear it after that change,
and a range a listener reports while it hears one SHALL stand where it was reported. The selection a step leaves
SHALL be its last edit's, and undo SHALL put back the one its first edit found. A step of several edits
SHALL never join a run of typing.

#### Scenario: A move is one step of two edits

- **WHEN** the second of three lines is moved up with ⌥↑
- **THEN** `Changed` is raised once, with two replacements, one undo puts the three lines back as they were
  with the caret where it stood, and one redo moves the line again

#### Scenario: A listener follows every replacement

- **WHEN** a problem is reported on the second of three lines, the line is moved up, then undone and redone
- **THEN** the problem stands on the moved line's text after the move, after the undo and after the redo

#### Scenario: A listener that edits as it hears a step

- **WHEN** a listener inserts a line at the document's start as it hears a move, or its undo
- **THEN** the move, or the undo, is made whole below the inserted line, and the inserted line is an undo
  step of its own

#### Scenario: Every listener hears the replacements in order

- **WHEN** a problem is reported on the second of three lines, and a listener inserts a line below the
  first as it hears the second line moved up
- **THEN** the problem stands on the moved line's text

#### Scenario: A problem reported while a change is heard

- **WHEN** a listener reports a problem over the moved line's text as it hears the second of three lines
  moved up
- **THEN** the problem stands where it was reported

#### Scenario: The selections heard

- **WHEN** a line is moved up, the caret is placed elsewhere, and the move is undone
- **THEN** every replacement heard carries the caret placed as its selection before, and the selection the
  undo restored as its selection after

#### Scenario: Typing still joins a run

- **WHEN** "abc" is typed, a character at a time, then undone
- **THEN** one undo takes back the three characters
