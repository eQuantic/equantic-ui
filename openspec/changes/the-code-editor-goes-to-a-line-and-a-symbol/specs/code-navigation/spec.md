## ADDED Requirements

### Requirement: Going to a line

`GoToLine` SHALL open the go-to field with `:`. The text after it, trimmed, SHALL be read as a line and an
optional column split by `,`, `:` or `#`, each read as JavaScript's `parseInt` reads it. A negative line
SHALL count from the end, a line past either end SHALL be the nearest one, and the column SHALL be a
visible column, clamped to the line, a negative one counting from its end, and with none the line's start.
While a line is read, it SHALL be centred in the view and washed, and the selection SHALL NOT move.

#### Scenario: A line and a column

- **WHEN** a document of 100 lines whose line 40 reads `\tx = 1;` is open, ⌃G pressed and `40:5` typed
- **THEN** line 40 is centred and washed, the caret has not moved, and Enter puts it before the `x`, the
  fifth visible column after the tab's four cells

#### Scenario: Counting from the end

- **WHEN** a document of 100 lines is open, ⌃G pressed, `-1` typed and Enter pressed
- **THEN** the caret stands at the start of line 100

#### Scenario: Past the end

- **WHEN** a document of 100 lines is open, ⌃G pressed, `250` typed and Enter pressed
- **THEN** the caret stands at the start of line 100

### Requirement: Going to a symbol

`GoToSymbol` SHALL open the go-to field with `@`. Whenever the field turns to symbols, every symbol provider
SHALL be asked once, and each answer flattened depth-first, every symbol carrying its parent's name as its container. The text after `@`, trimmed, SHALL
filter them: its first word against the name and the others against the container, ranked by score and
then by place in the document. With nothing typed, every symbol SHALL be listed in document order, the
last one whose range holds the caret selected. The selected entry SHALL be centred and its range washed,
the selection unmoved, and Enter SHALL select the start of its `SelectionRange`.

#### Scenario: A method by its initials

- **WHEN** a provider answers a class `Ledger` holding methods `AddEntry` and `Balance`, ⇧⌘O is pressed,
  `ae` typed and Enter pressed
- **THEN** `AddEntry` was listed first, its container `Ledger`, and the caret stands where its name
  starts

#### Scenario: The symbol the caret is in

- **WHEN** the caret is inside `Balance`'s body and ⇧⌘O is pressed
- **THEN** every symbol is listed in document order and `Balance` is selected

#### Scenario: One prefix becomes the other

- **WHEN** ⌃G is pressed, the field's text replaced with `@bal` and Enter pressed
- **THEN** the caret stands where `Balance` starts

### Requirement: Escape gives the view back

Escape in the go-to field SHALL close it, scroll the view back to where it stood when the field opened,
leave the selection as it was and give the code the keyboard. Accepting SHALL close it too, and with no
entry to go to SHALL do nothing.

#### Scenario: A preview abandoned

- **WHEN** the view shows lines 1 to 30, ⌃G is pressed, `400` typed and Escape pressed
- **THEN** the view shows lines 1 to 30, the caret is where it was and the code has the keyboard

### Requirement: The jump list

Every move of the primary caret SHALL be heard: a place whose top line is ten lines or more from the
current entry's SHALL be a new entry, a nearer one SHALL replace the current entry, and a jump SHALL always
be a new entry, after the place it left. `NavigateBack` and `NavigateForward` SHALL select the entry before
and after, record nothing, and centre it when it is out of view. A new entry after going back SHALL drop
the entries ahead, at most fifty SHALL be kept, and every entry SHALL be carried through every edit.

#### Scenario: Back from a jump

- **WHEN** the caret is on line 3, ⌃G is pressed, `80` typed, Enter pressed, and ⌃- pressed
- **THEN** the caret is back on line 3, and ⌃⇧- puts it on line 80

#### Scenario: A short move replaces the place

- **WHEN** the caret moves from line 3 to line 5, then to line 50, and ⌃- is pressed
- **THEN** the caret is on line 5

#### Scenario: A place carried through an edit

- **WHEN** the caret is on line 3, a jump goes to line 80, ⌃- is pressed, two line breaks are typed at the
  start of line 3, and ⌃⇧- is pressed
- **THEN** the caret is on line 82, the line that was 80

### Requirement: Revealing in the middle of the view

`RevealInCenter` SHALL scroll the view so that the position's line is in its middle, and
`RevealInCenterIfOutsideView` SHALL do so only when the line is not in view. Neither SHALL move the caret,
and before the view is known neither SHALL scroll.

#### Scenario: A line far below

- **WHEN** a view 300 high over lines 20 high shows the top of the document and the line at index 100 is
  revealed in the center
- **THEN** the view's top is at 100 × 20 + 10 − 150 = 1860

### Requirement: The keys

The defaults SHALL bind `GoToLine` to ⌃G on Apple and Ctrl+G elsewhere, `GoToSymbol` to ⇧⌘O and
Ctrl+Shift+O, `NavigateBack` to ⌃- on Apple and Alt+← elsewhere, and `NavigateForward` to ⌃⇧- on Apple
and Alt+→ elsewhere. While the go-to field shows, ↑ and ↓ SHALL select the entry above and below, PageUp
and PageDown a page away, Enter SHALL accept and Escape SHALL close it.

#### Scenario: Walking the symbols

- **WHEN** ⇧⌘O is pressed over three symbols with the first selected, and ↓ pressed twice and ↑ once
- **THEN** the second symbol is selected, centred and washed

### Requirement: What reaches the code while the field shows is the field's

While the go-to field shows, text that reaches the code, typed or pasted, SHALL go on the field's text, and
Enter, Escape, the arrows, the page keys and Backspace that reach the code SHALL act on the field, so
nothing typed before the field takes the keyboard edits the code. A composition SHALL NOT start in the code,
a cut SHALL take nothing, and a chord with ⌘, ⌃ or ⌥ SHALL be left to the host.

#### Scenario: A line number typed at once

- **WHEN** ⌃G is pressed and `120` typed and Enter pressed before the field has the keyboard
- **THEN** the caret stands at the start of line 120 and the code is as it was
