# code-cursors Specification

## Purpose
What a code editor does with several carets, on both targets and with VS Code's keys: it adds them by key, by pointer and by occurrence, selects a column, runs every command at every caret as one undo step, spreads a paste and a completion over them, makes one of the carets that meet, and draws them all, the primary the one placed last.

## Requirements

### Requirement: Several selections, one of them primary

A code editor SHALL hold one or more selections, `Selections`, the primary first and the others in
document order. Two selections that overlap SHALL be made one, and so SHALL a caret where another selection
begins, ends or stands; two selections that only touch SHALL stay two, as VS Code merges its cursors. The
primary SHALL be the selection placed last: the one brought into view, the one an input method composes
at, and the one the completion list, the signature panel and the card follow. Setting `Selection` SHALL
leave that one selection, and SHALL be heard as a change of the selection though the primary stays. Each
caret SHALL keep the cell a run of ↑ and ↓ aims at, and every caret SHALL move with an arrow.

#### Scenario: Two carets that meet

- **WHEN** there are carets at the end of the second and third of three lines of equal length, and ↑ is
  pressed twice
- **THEN** after the first press there are carets on the first and second lines, and after the second one
  caret is left, on the first line

#### Scenario: Setting the selection

- **WHEN** there are three carets, and the app sets `Selection` to a range
- **THEN** that range is the only selection

#### Scenario: Each caret keeps its cell

- **WHEN** there are carets at column 5 of a line and at column 1 of a line two below, the line between
  them two characters long, and ↓ then ↑ are pressed
- **THEN** the carets are where they started

### Requirement: Adding carets by key

⌥⌘↑ and ⌥⌘↓ (Ctrl+Alt+↑ and Ctrl+Alt+↓ on Windows and Linux) SHALL add a caret on the line above or below
every caret, at the cell it aims at, a caret that would stand where one is made one with it, and the caret
added furthest that way SHALL be the primary. With every caret on the first line going up, or on the last
going down, the key SHALL do nothing and SHALL NOT be claimed.

#### Scenario: Three carets in a column

- **WHEN** the caret is at column 4 of the second line, and ⌥⌘↓ is pressed twice
- **THEN** there are carets at column 4 of the second, third and fourth lines, the one on the fourth line
  the primary

### Requirement: Adding carets by pointer

⌥ and a press SHALL add a caret where it lands, as the primary, or, while there are others, take away the
caret it lands on. ⇧⌥ and a press SHALL select the column from the primary's anchor to the cell pressed,
and a drag SHALL grow or narrow it: one selection on each line, from the anchor's cell to the cell pressed;
a line that ends before the box's left edge SHALL have none unless every line does. The selection on the
line pressed SHALL be the primary, or the one nearest it when that line has none.

#### Scenario: A column over lines of different lengths

- **WHEN** the caret is at column 2 of `abcdef`, and ⇧⌥ and a press land at column 4 of the third line, the
  second line being `a`
- **THEN** columns 2 to 4 of the first and third lines are selected, the third line's the primary, and the
  second line has no selection

#### Scenario: A caret taken away

- **WHEN** there are two carets, and ⌥ and a press land on one of them
- **THEN** the other is the only caret

### Requirement: Occurrences

⌘D (Ctrl+D) with the primary empty SHALL select the word at every empty caret, and its later presses SHALL
search by whole word and by case; with a selection made otherwise it SHALL search for the selected text,
case kept. Each press SHALL add the next match after the primary, wrapping at the document's end, as the
new primary, and SHALL be claimed while there is a word or a selection to search, every match taken
included. ⇧⌘L (Ctrl+Shift+L) SHALL select every match, the one the caret stood in the primary.

#### Scenario: A word in three places

- **WHEN** the caret is in the first `count` of a text that holds `count` three times and `counter` once, and
  ⌘D is pressed three times
- **THEN** the three `count` are selected, the last added the primary, and `counter` is not; a fourth press
  is claimed and adds nothing

#### Scenario: Every occurrence

- **WHEN** the caret is in the second `a` of `a b a` and a line `a`, ⇧⌘L is pressed and `z` typed
- **THEN** the text reads `z b z` and `z`

### Requirement: Escape goes back to one caret

Escape SHALL close the completion list, then the card, then the signature panel, then take away every caret
but the primary, and only then release Tab, one at a time.

#### Scenario: Escape with three carets

- **WHEN** there are three carets and nothing shows over the code, and Escape is pressed
- **THEN** the primary alone is left, the key is claimed, and Tab still indents

### Requirement: Every command at every caret

Typing, deleting, Enter, Tab and ⇧Tab, ⌘/, the line operations, ⌘↩, a cut, a paste and an accepted
completion SHALL act at every caret, as ONE undo step heard as one change. A command written for one caret
SHALL do at each caret what it does at that caret alone, from the last caret to the first, each caret
placed where its own run left it. A later caret's edit that takes an earlier caret's text SHALL take that
caret with it, and a word deleted to the right SHALL stop where the next caret's text begins, as VS Code
keeps the first of two deletions that would overlap. The line commands SHALL take the lines of every
caret, the carets whose lines share a line as one block, and for a move also the ones whose lines follow
each other; Tab, ⇧Tab and ⌘/ over lines SHALL be an edit a line, so a line two carets stand on is indented
once and a problem on it travels with its text.

#### Scenario: Two carets on one line

- **WHEN** there are carets at columns 1 and 3 of `abcd`, and Backspace is pressed
- **THEN** the text is `bd`, with carets at columns 0 and 1

#### Scenario: A word deleted left over another caret

- **WHEN** there are carets at columns 1 and 3 of `foo bar`, and ⌥⌫ is pressed
- **THEN** the text is ` bar`, with one caret at its start

#### Scenario: A word deleted right up to another caret

- **WHEN** there are carets at columns 0 and 2 of `foo bar`, and ⌥⌦ is pressed
- **THEN** the text is `bar`, with one caret at its start

#### Scenario: Tab with two selections on one line

- **WHEN** two selections stand on the first line, and Tab is pressed
- **THEN** the line is indented once, and both selections keep their text

#### Scenario: Two blocks moved

- **WHEN** there are carets on the second and fourth of five lines, and ⌥↑ is pressed
- **THEN** each of those lines changes places with the line above it, and one undo puts both back

### Requirement: The clipboard at every caret

A copy with several carets SHALL take every selection's text, a line each, or every caret's line when none
selects anything, each line once. A paste of what a copy at as many carets took SHALL give each caret its
own piece, a text with one line for each caret, its last line break aside, SHALL give each caret its line,
and any other text SHALL go whole to every caret. A cut SHALL take at every caret what a copy takes.

#### Scenario: A paste spread over the carets

- **WHEN** there are three carets, and the text `a\nb\nc\n` is pasted
- **THEN** the first caret has `a`, the second `b` and the third `c`

#### Scenario: A copy pasted back at as many carets

- **WHEN** `one` and `three` are selected at two carets and copied, two carets are placed, and the copy is
  pasted
- **THEN** the first caret has `one` and the second `three`

### Requirement: A completion accepted at every caret

An entry accepted with several carets SHALL replace, around every caret, what it replaces around the
primary, on the caret's own line, as VS Code applies one suggestion at every cursor, as one undo step. The
word being completed and a provider's range SHALL stand where the edits made since, at every caret, carried
them, what is typed at the range's start or end being in it.

#### Scenario: An entry at two carets

- **WHEN** there are carets at the end of `x = ` and `y = `, `Co` is typed and the list's `Column` accepted
- **THEN** the lines read `x = Column` and `y = Column`, and one undo gives back `Co` at both

#### Scenario: A word moved by a caret before it

- **WHEN** there are carets after both words of `Con Con`, the second the primary, the list is asked for,
  Backspace is pressed and `ConsoleOutput` accepted
- **THEN** the line reads `ConsoleOutput ConsoleOutput`

### Requirement: Every caret is drawn

Every caret SHALL be drawn, the primary first in the surface's carets, and every selection's bands and
every caret's line in the code's own layers, on the web and on Photon; a view SHALL ask for the selections
and the lines of its window only, and the web surface's input SHALL stay the element it is, and keep the
keyboard, as the carets change in number. An input method SHALL compose at the primary caret, the other carets
keeping their text as the composition grows, and what it commits SHALL be typed at every caret.

#### Scenario: The keyboard stays in the code

- **WHEN** carets are added on the web while a note appears over the code
- **THEN** the surface's input is the element it was, and it keeps the keyboard

#### Scenario: Two carets drawn on the web

- **WHEN** ⌘D is pressed twice over two `count` and `n` typed through the surface's input
- **THEN** the text holds `n` twice, and the lowered surface draws two carets
