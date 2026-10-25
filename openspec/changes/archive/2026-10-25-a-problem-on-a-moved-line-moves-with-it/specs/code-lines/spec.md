## MODIFIED Requirements

### Requirement: Move the lines

⌥↑ and ⌥↓ (Alt+↑, Alt+↓) SHALL move the lines one line up or down past the line beside them, the selection
moving with them. The line they pass SHALL be the one written again, on the block's other side, and deleted
where it was, as one undo step, so the moved lines keep their text where it was and a problem on them moves
with them; a problem on the passed line is left empty where its text was until it is reported again. Except
at the document's end, where no line follows the moved lines, neither replacement SHALL write where they
start or end, so a problem of no width there, a missing semicolon at a line's end, moves with them too. At the
document's first line a move up SHALL do nothing and SHALL NOT be claimed, and so SHALL a move down while the
selection reaches the document's last line, its end at the start of that line included, as VS Code's move
refuses it. While the signature panel shows more than one signature, the two keys SHALL walk the overloads
instead.

#### Scenario: A line down

- **WHEN** the caret is on line 1 of three, and ⌥↓ is pressed
- **THEN** line 1 changes places with line 2, and the caret is on the moved line at its column

#### Scenario: A moved line's problem

- **WHEN** a problem is reported on line 1 of three, the caret is on line 1, and ⌥↑ is pressed, then undone
  and redone
- **THEN** the problem stands over the same text on line 0 after the move, on line 1 after the undo, and on
  line 0 after the redo, and never covers the line passed

#### Scenario: A problem of no width at a moved line's end

- **WHEN** a problem of no width is reported at the end of line 1 of three, the caret is on line 1, and ⌥↑
  is pressed, then undone and redone
- **THEN** the problem stands at the end of the same text after each

#### Scenario: A selection reaching the last line

- **WHEN** lines 1 and 2 of three are selected from the start of line 1 to the start of line 3, and ⌥↓ is
  pressed
- **THEN** nothing moves, and the editor does not claim the key, as VS Code's move refuses it

#### Scenario: A block ending on a blank line

- **WHEN** the first two lines of `a`, an empty line, `c` and `d` are selected from the start of the first
  line to the start of the third, ⌥↓ is pressed and then ⌥↑
- **THEN** the text is as it was, the empty line included, and so is the selection

#### Scenario: At the top

- **WHEN** the caret is on the first line, and ⌥↑ is pressed
- **THEN** nothing changes, and the editor does not claim the key

#### Scenario: The panel's overloads

- **WHEN** the signature panel shows three overloads, and ⌥↓ is pressed
- **THEN** the next overload shows, and no line moves
