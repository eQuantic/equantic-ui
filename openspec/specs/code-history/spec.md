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
last first and redo SHALL make them again first first. The editor SHALL raise `Changed` once for each
change, an edit, a step, an undo and a redo alike, once all of its replacements are made, with all of them
in the order made, each range in the coordinates the replacements before it left, and each with the
selection the change found and the one it left. An edit a listener makes while it hears a change SHALL be
made on the text the change left, as a step of its own, and every listener SHALL hear it after that change,
and a range a listener reports while it hears one SHALL stand where it was reported. Undo SHALL put back every
selection the step found and redo every one it left, the primary first (#1012). A step of several edits at
one caret SHALL never join a run of typing, and a step of one typed character at each of several carets
SHALL join the run before it while the carets stand where the run left them.

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

#### Scenario: Typing at three carets

- **WHEN** there are carets at the start of three lines, and `x` then `y` are typed
- **THEN** each line starts with `xy`, one undo takes back all six characters and puts back the three
  carets, the primary first, and one redo types them again and puts back the carets it left
