# code-lines Specification

## Purpose
What a code editor does to whole lines, on both targets and with VS Code's keys: it moves, copies, deletes, joins and sorts the lines a selection takes, and opens a line above or below the caret's, each as one undo step that keeps the selection on the lines it moved.

## Requirements

### Requirement: The lines an operation takes

A line operation SHALL take the lines the selection touches, a selection that ends at column 0 leaving that
line out, or the caret's line when nothing is selected. A join SHALL take every line the selection touches,
that one included, as VS Code's join does. Each SHALL be ONE undo step, undo SHALL put back
the selection it found and redo the selection it left, and none SHALL run in a read-only editor. The
selection an operation leaves SHALL have no end inside a text element, as no selection placed any other way
has.

#### Scenario: A selection ending at a line's start

- **WHEN** lines 1 and 2 are selected from the start of line 1 to the start of line 3, and ⌥↓ is pressed
- **THEN** lines 1 and 2 move down, and line 3 goes above them

#### Scenario: A deleted line's column inside an emoji

- **WHEN** the caret is at column 1 of `ab`, the next line starts with an emoji, and ⇧⌘K is pressed
- **THEN** the caret stands at column 0 of the emoji's line, never between the emoji's two halves

#### Scenario: Undo and redo of a move

- **WHEN** a line is moved down, then undone, then redone
- **THEN** undo puts the line back with the caret where it was, and redo moves it again with the line
  selected as the move left it

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

### Requirement: Copy the lines

⇧⌥↑ and ⇧⌥↓ (Shift+Alt+↑, Shift+Alt+↓) SHALL copy the lines above or below themselves, the selection on
the upper copy for ⇧⌥↑ and on the lower one for ⇧⌥↓.

#### Scenario: A line copied down

- **WHEN** the caret is on line 1, and ⇧⌥↓ is pressed
- **THEN** line 1 is there twice, and the caret is on the second, at its column

### Requirement: Delete the lines

⇧⌘K (Ctrl+Shift+K) SHALL delete the lines, their line break included, the caret on the line that takes
their place at the column it had, or on the line before when the last lines go.

#### Scenario: A line in the middle

- **WHEN** the caret is at column 2 of line 1 of three, and ⇧⌘K is pressed
- **THEN** two lines are left, and the caret is at column 2 of what was line 2

#### Scenario: The only line

- **WHEN** the document is one line, and ⇧⌘K is pressed
- **THEN** the document is empty

### Requirement: Join the lines

⌃J on a Mac SHALL join the lines into one, or the caret's line with the next when nothing spanning lines is
selected, as VS Code joins them: each next line's leading spaces and tabs go, one space joins the text unless
the text before is empty or ends with a space or a tab, which collapse to one space, and a next line of spaces
and tabs alone adds nothing. No other character is white space to a join: a no-break space is text. With
nothing selected the caret SHALL stand where the lines met; with lines selected the selection's end SHALL
keep its place in its text, as VS Code's join keeps it, and an end in the first line's white space that the
join collapsed SHALL stand after the one space left. On the last line, with nothing selected, there is
nothing to join and the key SHALL NOT be claimed. On Windows and Linux
`CodeCommand.JoinLines` has no default key, as in VS Code.

#### Scenario: A line and the next

- **WHEN** line 1 is `int a =` and line 2 is `    1;`, the caret is on line 1, and ⌃J is pressed
- **THEN** line 1 is `int a = 1;`, and the caret stands before the space

#### Scenario: A selection's end keeps its text

- **WHEN** `b` of `ab` and `c` of the next line `cdef` are selected, and ⌃J is pressed
- **THEN** the line is `ab cdef`, and `b c` is selected, `def` not

#### Scenario: A start in the collapsed white space

- **WHEN** a selection starts in the spaces that end `ab        ` and ends after the `c` of the next line
  `cdef`, and ⌃J is pressed
- **THEN** the line is `ab cdef`, and the `c` alone is selected

#### Scenario: A selection ending at a line's start

- **WHEN** lines 1 to 3 are selected from the start of line 1 to the start of line 3, and ⌃J is pressed
- **THEN** lines 1, 2 and 3 are one line

#### Scenario: A no-break space

- **WHEN** line 1 ends with a no-break space, line 2 starts with one, and ⌃J is pressed
- **THEN** both no-break spaces stay, one space between them

### Requirement: Sort the lines

`CodeCommand.SortLinesAscending` and `CodeCommand.SortLinesDescending` SHALL sort the lines, ordinally and
case aside, ties broken ordinally, the same on both targets, the selection kept. With fewer than two lines
there is nothing to sort. They have no default key, as in VS Code.

#### Scenario: Three lines ascending

- **WHEN** the lines `cherry`, `apple` and `Banana` are selected, and the lines are sorted ascending
- **THEN** they read `apple`, `Banana`, `cherry`

### Requirement: Insert a line above or below

⌘↩ (Ctrl+Enter) SHALL insert a line below the caret's line and ⇧⌘↩ (Ctrl+Shift+Enter) one above it,
wherever the caret stands on its line, the new line indented as the caret's line, and below a line that
opens a block one step more, as Enter indents; the caret SHALL go to the new line's indentation.

#### Scenario: Below a block's opening

- **WHEN** the caret is in the middle of `if (x) {`, and ⌘↩ is pressed
- **THEN** a line follows it, indented one step more, the caret at that indent, and `if (x) {` is whole
