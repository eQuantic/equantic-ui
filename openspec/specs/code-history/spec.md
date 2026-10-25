# code-history Specification

## Purpose
What a code editor's edit records, what its listeners hear and what undo and redo restore: the
text the document holds, its lines broken where the document breaks them.

## Requirements

### Requirement: An edit records the text the document holds

The edit a code editor records and announces SHALL carry the inserted text as the document holds it,
its lines broken where the document breaks them, and its range SHALL end where that text ends.

#### Scenario: A paste with CR and CRLF

- **WHEN** "x\r\ny\rz" is pasted into "abc" after its first character
- **THEN** the edit carries "x\ny\nz", and its range ends at the caret

#### Scenario: An edit built by hand

- **WHEN** an edit inserting "x\ry" at line 0, column 1 is built by hand
- **THEN** its inserted range ends at line 1, column 1, and it is not a simple insert

#### Scenario: A typed carriage return ends a run of typing

- **WHEN** a typed edit inserting "\r" after the "a" of "ab" is recorded by hand, then a typed "x"
  where it ends
- **THEN** one undo takes back the "x" alone

### Requirement: Undo after a paste restores the document exactly

Undo after a paste SHALL restore the document exactly, and redo SHALL replay it, whatever breaks the
pasted lines.

#### Scenario: Line breaks of every kind

- **WHEN** "x\ry", "\r", "x\r\ny", "x\ny" and "x\r\r\ny\n" are each pasted into "abc" after its
  first character, then undone and redone
- **THEN** undo gives "abc" back each time, and redo gives back the lines the paste made

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
