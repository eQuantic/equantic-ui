## ADDED Requirements

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
