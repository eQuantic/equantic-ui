# code-navigation Specification

## Purpose
How a code editor moves through its document, on both targets and with VS Code's keys: a field over the code that goes to a line, read as VS Code's "Go to Line" reads it, and to a symbol a language service declares, previewing in the code and giving the view back on Escape; the jump list that hears every move of the caret and walks back and forward; and revealing a position in the middle of the view.

## Requirements

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
entry to go to SHALL do nothing. A press in the code, and the field losing the keyboard, SHALL close it where
the view stands, the press landing where the person pointed.

#### Scenario: A press in the code

- **WHEN** the view shows lines 1 to 30, ⌃G is pressed, `120` typed and line 125 pressed
- **THEN** the field is closed, the view still shows line 120 in its middle, and the caret is on line 125

#### Scenario: A preview abandoned

- **WHEN** the view shows lines 1 to 30, ⌃G is pressed, `400` typed and Escape pressed
- **THEN** the view shows lines 1 to 30, the caret is where it was and the code has the keyboard

### Requirement: The jump list

Every move of the primary caret SHALL be heard: a place whose top line is ten lines or more from the
current entry's SHALL be a new entry, a nearer one SHALL replace the current entry, and a jump SHALL always
be a new entry, after the place it left. `NavigateBack` and `NavigateForward` SHALL select the entry before
and after, record nothing, and centre it when it is out of view. A new entry after going back SHALL drop
the entries ahead, at most fifty SHALL be kept, and every entry SHALL be carried through every edit in the
direction it was selected in.

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
nothing typed before the field takes the keyboard edits the code. A composition in flight SHALL be cancelled
as the field opens, none SHALL start in the code while it shows, a cut SHALL take nothing, and a chord with ⌘,
⌃ or ⌥ SHALL be left to the host.

#### Scenario: A line number typed at once

- **WHEN** ⌃G is pressed and `120` typed and Enter pressed before the field has the keyboard
- **THEN** the caret stands at the start of line 120 and the code is as it was

### Requirement: Asking where a name is defined and used

`CodeEditorController.Definitions` SHALL ask every `ICodeDefinitionProvider` in its `Providers` at once, for a
position and a `CodeDefinitionKind`, and `CodeEditorController.References` every `ICodeReferenceProvider` in
its own. The answer SHALL wait for the last provider, a provider that throws SHALL count as answering nothing
and SHALL be reported through `Failed`, and the places SHALL be ordered by document, the editor's own (a null
`Document`) first and the others by name, ordinally, then by where they start, a place equal to the one
before it dropped. A request SHALL be dropped when the text changes or the caret moves before it is answered.

#### Scenario: Two providers answer

- **WHEN** one definition provider answers line 30 and line 4 of the document, and another answers line 4 and
  a place in `Other.cs`
- **THEN** the places are line 4, line 30 and the place in `Other.cs`, in that order

#### Scenario: The caret moves while it is asked

- **WHEN** F12 is pressed, the caret moved before the provider answers, and the provider then answers one place
- **THEN** the caret stays where it was moved and nothing is listed

### Requirement: Going to a definition

`GoToDefinition`, `GoToDeclaration`, `GoToTypeDefinition` and `GoToImplementation` SHALL ask the definitions
of their kind at the caret. One place in the document SHALL be gone to: the caret at its start, the move
recorded as a jump, and the place brought near the top of the view when it is out of it. Several SHALL be
listed. With none, a card at the caret SHALL say that nothing was found for the word there, in VS Code's words
for the kind. When the caret already stands in the only place answered for a definition, a declaration or a
type definition, the references SHALL be found instead.

#### Scenario: One definition

- **WHEN** the caret stands on a use of `total` at line 20, a provider answers its declaration at line 10,
  and F12 is pressed
- **THEN** the caret stands at the start of `total` on line 10, and ⌃- puts it back on line 20

#### Scenario: No definition

- **WHEN** the caret stands on `amount` and the providers answer nothing for F12
- **THEN** a card at the caret says "No definition found for 'amount'", and moving the caret closes it

#### Scenario: On the definition itself

- **WHEN** the caret stands on the declaration of `total`, the only place F12 is answered, and F12 is pressed
- **THEN** the references of `total` are found, as ⇧F12 finds them

### Requirement: Revealing near the top of the view

`CodeEditorController.RevealNearTopIfOutsideView` SHALL leave the view alone when the position's line is in
it, and otherwise scroll so that the line stands a fifth of the view below its top, five lines at the least,
without pushing the line out of the view.

#### Scenario: A line below the view

- **WHEN** a view of 40 lines shows lines 1 to 40 and the caret goes to line 200 through a definition
- **THEN** the view's first line is line 192, eight lines above it

### Requirement: Finding the references

`FindReferences` SHALL ask the references at the caret. One place SHALL be gone to as a definition is, several
SHALL be listed, and with none a card at the caret SHALL say that no references were found for the word.

#### Scenario: Several references

- **WHEN** a provider answers four places for `total` and ⇧F12 is pressed with the caret on its third use
- **THEN** the list shows four rows, the third selected, centred and washed, and the caret has not moved

### Requirement: The list of places

The go-to field SHALL list places in a `Definitions` or `References` mode that its text does not choose: the
text SHALL filter the rows by what they show, with the completion's matcher, keeping the document's order. A
row SHALL show the place's line with its leading white space left out and the name marked, and its line and
column; a place in another document SHALL show the document's name and its line and column. With nothing
typed, the place nearest the caret SHALL be selected: one in the same document before any other, then the
nearest by lines and columns. A selected place in the editor's document SHALL be centred and its lines washed,
the caret unmoved. Enter SHALL go to the selected place as a single answer is gone to, and Escape SHALL give
back the view as it was when the list opened.

#### Scenario: Filtering the rows

- **WHEN** four references are listed, two of them on lines that read `total += amount;`, and `+=` is typed
- **THEN** two rows remain, in the document's order, the first selected

#### Scenario: Going to a row

- **WHEN** four references are listed, ↓ is pressed and Enter
- **THEN** the caret stands at the start of the reference after the one that was selected, and ⌃- goes back
  to where the caret was before ⇧F12

### Requirement: Walking the places

The places of the last list SHALL be held by the editor, carried through every edit, until another list
replaces them or the document is replaced. `NextReference` and `PreviousReference` SHALL select the next and
the previous row while the list shows, wrapping, and once it is closed SHALL go to the place after or before
the one last gone to, wrapping, each move a jump. With no place held they SHALL do nothing, and their keys
SHALL be left to the host.

#### Scenario: Walking after the list closed

- **WHEN** four references at lines 10, 20, 30 and 40 are listed, Enter goes to the one at line 20, a line is
  inserted at the top of the document, and F4 is pressed twice
- **THEN** the caret stands at the reference that was at line 40, now at line 41

### Requirement: A place in another document

Going to a place in another document SHALL raise `CodeEditorController.OpenRequested` with its location, and
`CodeEditor.OnOpenRequested` with it, and SHALL leave the caret, the selection and the jump list as they were.

#### Scenario: A definition in another file

- **WHEN** a provider answers one place, in `src/Console.cs` at line 8, and F12 is pressed
- **THEN** `OpenRequested` is raised once with `src/Console.cs` and that range, and the caret has not moved

### Requirement: Following a name with ⌘

While the engine's Command modifier (⌘ on Apple, Ctrl elsewhere) is held alone and the pointer rests on a
word, the definitions there SHALL be asked at once, once for each word, and an answer with a place SHALL show
the link: the word underlined, and the pointer a hand over it. The link SHALL go when the modifier is
released, another key is pressed, the pointer leaves the word or the surface, a press starts a drag, the text
changes, or the view scrolls. A single press with the modifier alone SHALL place the caret as a press does,
and its release on the same line without a drag SHALL go to the definition at the press, saying nothing when
there is none, and take the link away.

#### Scenario: A link and a click

- **WHEN** ⌘ is pressed with the pointer at rest on a use of `total` whose declaration a provider answers,
  and the pointer is pressed and released there
- **THEN** `total` is underlined and the pointer is a hand before the press, and after the release the caret
  stands at the start of the declaration

#### Scenario: A drag with ⌘

- **WHEN** ⌘ is held, the pointer pressed on `total`, dragged two lines down and released
- **THEN** the selection runs from the press to the release, and the caret has not gone to the definition

### Requirement: The keys of definitions and references

The defaults SHALL bind `GoToDefinition` to F12, `GoToImplementation` to ⌘F12 on Apple and Ctrl+F12
elsewhere, `FindReferences` to ⇧F12, and `NextReference` and `PreviousReference` to F4 and ⇧F4, in both
conventions. `GoToDeclaration` and `GoToTypeDefinition` SHALL have no key, as VS Code gives them none. While
the list shows, F4 and ⇧F4 SHALL walk its rows.

#### Scenario: F4 with nothing held

- **WHEN** no list has been shown and F4 is pressed
- **THEN** the key is not claimed
