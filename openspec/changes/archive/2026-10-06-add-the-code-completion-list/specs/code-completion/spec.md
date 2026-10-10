# Spec Delta

## ADDED Requirements

### Requirement: The editor draws the list at the word it completes

While its completion shows a list, `CodeEditor` SHALL draw it one line below the word being
completed, in the code's own coordinate space, with its labels lined up with the word and its rows the
editor's line height. When a page of rows does not fit below the line inside the editor's viewport and
fits above it, the list SHALL be drawn above the line; when neither side fits, on the larger side with
as many rows as fit, never fewer than one. The list's right edge SHALL stay inside the viewport, and its
left edge too when the code scrolls sideways. It SHALL show at most a page of rows, and the selected row
SHALL stay among them as the selection moves. An answer that arrives after the keystroke SHALL be drawn
without waiting for another key. The selected entry's documentation, drawn on the list's side away from
the line, SHALL take the room the rows leave on that side and no more, as many of its lines as fit or
none, and the rows SHALL NOT yield to it.

#### Scenario: A word typed near the top

- **WHEN** `Co` is typed, indented, on the second line of an editor 20 lines tall
- **THEN** the list's top is the bottom of the caret's line, and it stands as far left of the word as
  its labels stand inside it

#### Scenario: A word typed on the last visible line

- **WHEN** `Co` is typed on the last line the editor shows
- **THEN** the list's rows end at the top of the caret's line

#### Scenario: A short file in a tall pane

- **WHEN** `I` is typed on the second line of a two-line file in an editor that fills a pane 400 tall,
  with thirty entries to offer
- **THEN** a page of twelve rows shows under the line, and every one of them can be pressed

#### Scenario: A word near the right edge

- **WHEN** `Co` is typed where a list standing at the word would run past the viewport's right edge
- **THEN** the list's right edge is the viewport's

#### Scenario: Code scrolled sideways

- **WHEN** a list is open on a short line and the code is scrolled 200 sideways
- **THEN** the list stands inside the viewport, at its left edge

#### Scenario: An answer after the key

- **WHEN** `Co` is typed and the provider answers only afterwards
- **THEN** the answer asks for a frame, and the list shows in it

#### Scenario: Walking past the page

- **WHEN** a list of 30 entries shows a page of 12 and ↓ is pressed 14 times
- **THEN** the fifteenth entry is selected and shown, the first is not shown, and PageDown steps by 12

#### Scenario: A documented row that just fits under its line

- **WHEN** `Col` is typed on the lowest line with room for one row under it, and `Column` has four
  lines of documentation
- **THEN** the row stands under the line, and none of its documentation shows

#### Scenario: A documented page above its line

- **WHEN** `I` is typed on the last visible line of an editor 300 tall, with twelve entries that each
  have four lines of documentation
- **THEN** all twelve rows show above the line, and the documentation shows the lines that fit between
  them and the view's top, cut

### Requirement: A row shows what the word matched

A row SHALL show the entry's kind, its label with the characters the word matched marked, and its
detail, and SHALL be named by its label and its detail. A row longer than the list SHALL cut its
detail before its label, each with an ellipsis, by the cells the code's grid gives its text elements
(a wide character or an emoji takes two) and between two of them, and its name SHALL keep both whole. The selected entry's documentation, once its provider resolved it, SHALL show with the list,
laid out only as far as it shows.

#### Scenario: Three letters of Column

- **WHEN** `Col` is typed and `Column` is listed
- **THEN** its row marks the first three characters of the label, in the code's face

#### Scenario: A row's name

- **WHEN** `Column`, a class with the detail `class Column`, is listed
- **THEN** its row is named `Column, class Column`

#### Scenario: A row longer than the list

- **WHEN** a 72-character label with the detail `string`, and `Column` with a detail longer than the
  list, are listed
- **THEN** the long label is cut with an ellipsis and its detail is not drawn, `Column`'s detail is cut
  with an ellipsis, and the long row's name still reads `<label>, string`

#### Scenario: An emoji at the cut, and wide characters

- **WHEN** a label whose sixtieth cell falls inside an emoji, and a label of forty wide characters, are
  listed
- **THEN** each is cut with an ellipsis before the text element that does not fit, no half of a
  surrogate pair is drawn, and neither takes more than the list's sixty columns

#### Scenario: The selected entry's documentation

- **WHEN** the list shows `ColorToken`, which has no documentation, and `Column`, which has, and ↓
  selects `Column`
- **THEN** `Column`'s documentation shows with the list, and none showed while `ColorToken` was
  selected

### Requirement: A press on a row accepts it, and the keyboard stays in the code

A press on a row SHALL select that entry and accept it, and SHALL leave the keyboard in the code: the
editor keeps the focus, and no list closes because the editor lost it. The app SHALL hear of the edit
as it does of a key's. A row SHALL be no Tab stop, and a press on the list that no row takes SHALL
move no caret.

#### Scenario: Pointing at the second entry

- **WHEN** `Co` is typed, the list shows `ColorToken`, `Column` and `Count`, and the second row is
  pressed
- **THEN** the line reads `Column`, the caret is after it, and the editor still has the keyboard

#### Scenario: A press between the rows

- **WHEN** the list's frame is pressed where no row is
- **THEN** the caret stays where the word left it, and the list stays open

### Requirement: The list is the code input's listbox

On the web, while a list shows, the code input SHALL say it completes from it, name it and point at
the selected option; the rows SHALL be options, the selected one selected. On Photon the rows SHALL
announce as options after the code field, the selected one selected.

#### Scenario: The second entry selected, on the web

- **WHEN** the list shows two entries and ↓ is pressed
- **THEN** the input's active descendant is the second option, and the input names the list it
  completes from

#### Scenario: The second entry selected, on Photon

- **WHEN** the list shows three entries and ↓ is pressed
- **THEN** the three rows announce as options after the code field, the second one selected

### Requirement: An editable editor completes the language's words and the document's

`CodeEditor.Completions` SHALL say what the editor completes from: null, the default, the language's
words and the document's; an empty list, nothing. A read-only editor SHALL complete nothing, and an
editor turned read-only SHALL close the list it shows and drop an answer still on its way. The editor
SHALL hand a list of the same providers to its completion only once, comparing the providers rather than
the list, so a list changed in place hands what it gained, and SHALL take out of it only the very
providers it put in, by reference.

#### Scenario: A new C# editor

- **WHEN** a `CodeEditor` holding `result = 1;` gets `re` typed on a new line
- **THEN** the list starts `readonly`, `record`, `ref`, `required`, `result` and `return`

#### Scenario: Completion turned off

- **WHEN** the same editor has `Completions = []` and `re` is typed
- **THEN** no list shows

#### Scenario: A provider the app added itself

- **WHEN** an app adds a provider to `Editor.Completion.Providers` before the editor's first build, and
  `Co` is typed
- **THEN** the list shows that provider's entries beside the document's words

#### Scenario: A parent that rebuilds

- **WHEN** a list is open and the parent rebuilds the editor with a new list around the same provider
- **THEN** the list stays open, and the completion's providers are the ones it already had

#### Scenario: A list changed in place

- **WHEN** a parent adds a provider to the list it handed the editor, and rebuilds it with that list
- **THEN** the completion's providers are the list's, the one it gained included

#### Scenario: A provider that equals the editor's

- **WHEN** an app adds a provider to `Editor.Completion.Providers` that equals the one the editor was
  given, and the parent rebuilds the editor with no providers
- **THEN** the app's provider stays, and the editor's is the one taken out

#### Scenario: Turned read-only while the list shows

- **WHEN** a list is open, or an answer is on its way, and the editor is turned read-only
- **THEN** the list closes, and the answer opens nothing when it arrives
