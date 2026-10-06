# Spec Delta

## ADDED Requirements

### Requirement: The editor draws the list at the word it completes

While its completion shows a list, `CodeEditor` SHALL draw it one line below the start of the word
being completed, in the code's own coordinate space, its rows the editor's line height. When a page of
rows does not fit below the line inside the editor's viewport and fits above it, the list SHALL be
drawn above the line; when neither side fits, on the larger side with as many rows as fit. The list's
right edge SHALL stay inside the viewport. It SHALL show at most a page of rows, and the selected row
SHALL stay among them as the selection moves.

#### Scenario: A word typed near the top

- **WHEN** `Co` is typed on the first line of an editor 20 lines tall
- **THEN** the list's top is one line below the caret's line and its left is the word's start

#### Scenario: A word typed on the last visible line

- **WHEN** `Co` is typed on the last line the editor shows
- **THEN** the list's bottom is the top of the caret's line

#### Scenario: Walking past the page

- **WHEN** a list of 30 entries shows a page of 12 and ↓ is pressed 14 times
- **THEN** the fifteenth entry is selected and shown, and the first is not shown

### Requirement: A row shows what the word matched

A row SHALL show the entry's kind, its label with the characters the word matched marked, and its
detail. The selected entry's documentation, once its provider resolved it, SHALL show with the list.

#### Scenario: Two letters of Column

- **WHEN** `Col` is typed and `Column` is listed
- **THEN** its row marks the first three characters of the label

### Requirement: A press on a row accepts it, and the keyboard stays in the code

A press on a row SHALL select that entry and accept it, and SHALL leave the keyboard in the code: the
editor keeps the focus, and no list closes because the editor lost it.

#### Scenario: Pointing at the second entry

- **WHEN** `Co` is typed, the list shows `Column` and `ColorToken`, and the second row is pressed
- **THEN** the text reads `ColorToken`, and the editor still has the keyboard

### Requirement: The list is the code input's listbox

On the web, while a list shows, the code input SHALL say it completes from it, name it and point at
the selected option; the rows SHALL be options, the selected one selected. On Photon the rows SHALL
announce as options after the code field, the selected one selected.

#### Scenario: The second entry selected, on the web

- **WHEN** the list shows three entries and ↓ is pressed
- **THEN** the input's active descendant is the second option, and the input says the list is open

### Requirement: An editable editor completes the language's words and the document's

`CodeEditor.Completions` SHALL say what the editor completes from: null, the default, the language's
words and the document's; an empty list, nothing. A read-only editor SHALL complete nothing.

#### Scenario: A new C# editor

- **WHEN** a `CodeEditor` holding `result = 1;` gets `re` typed on a new line
- **THEN** the list shows `readonly`, `record`, `ref`, `required`, `result` and `return`

#### Scenario: Completion turned off

- **WHEN** the same editor has `Completions = []` and `re` is typed
- **THEN** no list shows
