# Spec Delta

## Purpose

Two versions of a text and what differs between them, drawn by the write-once `CodeDiff` over rows
that are not all lines of its documents, the same on the web and on Photon.

## ADDED Requirements

### Requirement: The two sides stay level

Side by side, a `CodeDiff` SHALL pad the side with fewer lines at every change, so the first line
after a change is on the same row on both sides.

#### Scenario: A line replaced by two

- **WHEN** a diff compares `a`, `b`, `c` with `a`, `X`, `Y`, `c`
- **THEN** each side draws its own `c`, and the two are at the same height, the original on the left

### Requirement: Inline, the removed lines stand before what replaced them

Inline, a `CodeDiff` SHALL draw the lines a change removed between the lines that replaced them, in
one column, with the original's line numbers beside the modified's.

#### Scenario: A line replaced

- **WHEN** an inline diff compares `a`, `b`, `c` with `a`, `X`, `c`
- **THEN** `a`, `b` and `X` are drawn in that order, in one column

### Requirement: An unchanged run folds into a row that opens

A run of unchanged lines longer than `Context` on each side of a change SHALL fold into one row that
says how many lines it holds, and a press on that row SHALL open the run on both sides and leave the
caret where it was.

#### Scenario: A press on a fold

- **WHEN** a diff of 100 lines with one change at line 90 is drawn, and its fold is pressed through
  the host's own dispatch
- **THEN** line 10 is not drawn before the press, is drawn on both sides after it, and the caret did
  not move

#### Scenario: A fold with no words of its own

- **WHEN** a code block draws a fold its caller gave no label
- **THEN** the row says how many lines it hides, and so does its pressable's accessible name

### Requirement: The steps reach every change and wrap

F7 and the toolbar's next button SHALL move both carets to the next change after the modified caret,
Shift+F7 and the previous button to the one before it, wrapping at either end, and each step SHALL
reveal the change it reaches.

#### Scenario: Two changes

- **WHEN** a diff opens with changes at lines 10 and 80, and next is pressed twice
- **THEN** the caret is on line 10 when it opens, on line 80 after the first press, and back on line
  10 after the second

### Requirement: The modified side edits and is compared again

When a `CodeDiff` compares two texts and is not `ReadOnly`, its modified side SHALL take edits,
compare the edited document again, and call `OnChanged` with the whole text. A patch's view SHALL
read only.

#### Scenario: A character typed

- **WHEN** `x` is typed at the start of the modified side of a diff of `a`, `b` against itself
- **THEN** `OnChanged` receives `xa` and `b` on two lines, and the toolbar counts one line added and
  one removed

#### Scenario: A patch

- **WHEN** a diff opens one file of a patch with two hunks
- **THEN** its editor reads only, each side shows the second hunk's header where the patch leaves
  lines out, and its lines are numbered as the file numbers them
