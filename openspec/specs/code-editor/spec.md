# code-editor Specification

## Purpose
The write-once `CodeEditor` as a person presses it: where a press on its code puts the caret, and
which presses are not its code's, the same on the web and on Photon.

## Requirements

### Requirement: A press under a bounded editor's code is the end of the document

A bounded `CodeEditor` (one that fills its place or has a fixed height) SHALL draw its code as tall as
its viewport however short the file. A press under every row its code draws SHALL put the caret at
the end of the document and give the editor the keyboard, and a drag from there SHALL select back to
where it stops. A row after the last line, a diff's filler, SHALL take a press as any filler does: on
the line, at the press's column.

#### Scenario: Under a two-line file

- **WHEN** an editor 400 tall holding `one` and `two three` is pressed 300 below its top, under the
  second column of its last line
- **THEN** the caret is at the end of `two three`, and the editor has the keyboard

#### Scenario: A diff's fillers after the last line

- **WHEN** a side of three lines ends with two filler rows, and the second filler is pressed at the
  third column
- **THEN** the caret is on the last line at its third column, and a press below the fillers is at the
  end of the document

#### Scenario: A drag from under the code

- **WHEN** that press is dragged to the second column of the first line and released
- **THEN** the selection runs from the end of the document back to the second column of the first line

### Requirement: A press outside an editor is not its code's

A press SHALL land on an editor's code only where the code is on screen: the part of the code that runs
past the viewport showing it, and the part of a completion list that left the view with its line,
SHALL take no press.

#### Scenario: Under a scrolled editor

- **WHEN** an editor 200 tall holding 60 lines stands above a box, and the box is pressed 100 below the
  editor
- **THEN** the caret stays where it was, and the editor does not take the keyboard

### Requirement: Undo and redo raise the edit they made

`CodeEditorController.Changed` SHALL carry the edits an undo or a redo made, in one `CodeChange`: the range
each replaced, the text it removed and the text it wrote. A change with no edits, `Replaced`, SHALL mean
only a document replaced whole.

#### Scenario: Undoing a word

- **WHEN** `ab` is typed into an empty editor and undone
- **THEN** `Changed` carries one change whose one edit removed `ab` from 0:0 to 0:2 and wrote nothing

### Requirement: A code surface draws its notes over its code and caret

A `CodeSurface`'s `Notes` SHALL be positioned nodes, each drawn at its start and top in the surface's
coordinates, over its child and over its carets, on both targets, and the server SHALL write them where
the client does. While the pointer is over a note the realizer SHALL report nothing to the surface's
model, and a press on a note SHALL NOT move the caret. A note SHALL be clipped as the surface's options
are, and on a surface drawn under a transform it SHALL take the pointer in its own shape, not in the box
around it.

#### Scenario: A note over the caret

- **WHEN** a surface carries a note whose box covers the caret
- **THEN** the note is drawn over the caret, and a press on it leaves the selection where it was

#### Scenario: A turned surface

- **WHEN** a surface turned 30 degrees carries a note, and the pointer is in a corner of the box around
  the turned note that the note does not reach
- **THEN** the pointer is the code's there, and a press there moves the caret

### Requirement: The hosts report a pointer passing over a code surface and leaving it

Both realizers SHALL hand a code surface's model `PointerPhase.Hover`, with the pointer's position in
the surface's coordinates, when a pointer moves over the surface with nothing pressed, and
`PointerPhase.Exit` when it leaves the surface. A pointer moving with a press held SHALL hand the model
no `Hover`, wherever the press began, and a finger none at all. What is drawn over the code takes the
pointer before the surface does: a control, a field, the surface's options and its notes.

#### Scenario: A pointer passing over the code

- **WHEN** a mouse moves over a code surface with no button down and then leaves it
- **THEN** the model hears `Hover` at each position and then `Exit`

#### Scenario: A drag over the code

- **WHEN** a press held on a button above the editor moves over the code, and is released there
- **THEN** the model hears no `Hover` until the pointer moves with nothing pressed

### Requirement: A measured grid places text by its advance

The code engine SHALL place a caret, a selection band, a press and the column a vertical move aims at
by the measured advance of the line's text when its grid carries a measurer, and by its monospaced pitch
when it does not. Measured, a caret before a column SHALL stand where the text before it ends, a band
SHALL run from where its first character begins to where its last one ends, a press SHALL land on the
nearer side of the text element under it and never inside one, and ↑ and ↓ SHALL aim at the same
distance from the line's start rather than the same count of characters. A grid with no measurer SHALL
place everything exactly as before.

#### Scenario: A caret in a proportional face

- **WHEN** an editor whose grid measures `iii` as 9 points and `iiiW` as 21 holds the line `iiiWi`
  and its caret is placed after `W`
- **THEN** the caret stands 21 points from the line's start

#### Scenario: A press between two characters of different widths

- **WHEN** the same line is pressed 16 points from its start, nearer the end of `W` than its beginning
- **THEN** the caret stands after `W`

#### Scenario: A vertical move by distance

- **WHEN** a measured editor holds `WWWW` over `iiiiiiiiii`, where four `i` are as wide as one `W`,
  and ↓ is pressed with the caret after the first `W`
- **THEN** the caret stands after the fourth `i` of the second line

#### Scenario: The monospaced editor is unchanged

- **WHEN** a code editor whose grid carries no measurer holds a line with a tab, a wide character and
  an emoji
- **THEN** its carets, bands and presses land on the cells they landed on before this change

### Requirement: The hosts report the modifiers a pointer passes with

Every host SHALL hand a code surface the modifiers held with each pointer event, read as its keys are read
(⌘ is Command and ⌃ is Control on a Mac, Ctrl is Command elsewhere), and SHALL hand it a hover at the pointer's
last place whenever the held modifiers change while the pointer rests over it, wherever the keyboard is. That
place SHALL be the point on the screen the pointer stands on, read against the code as it stands then: a
press SHALL end the rest, so no hover is handed during it, and its release SHALL rest the pointer where it was
released. A press SHALL carry ⇧, ⌥, ⌃ and ⌘ alike.

#### Scenario: ⌘ pressed with the pointer at rest

- **WHEN** the pointer rests over a code surface in a Photon window on macOS and ⌘ is pressed
- **THEN** the surface hears a hover at the same place, with Command

#### Scenario: The code scrolled under a pointer at rest

- **WHEN** the pointer rests on `total`, the code scrolls a line under it, and ⌘ is pressed
- **THEN** the surface hears a hover over the name now under the pointer, on the next line

#### Scenario: ⌘ after a drag

- **WHEN** the pointer presses `total`, drags to `amount` and is released there, and ⌘ is pressed with no move
- **THEN** the surface hears a hover over `amount`, and `total` is not underlined

#### Scenario: ⌘ during a press

- **WHEN** the pointer presses the code and ⌘ is pressed before it is released
- **THEN** the surface hears no hover, and no link shows

#### Scenario: ⌥ and a press in a macOS window

- **WHEN** ⌥ is held and the code is pressed in a Photon window on macOS
- **THEN** a caret is added where it was pressed, as on the web

### Requirement: A code surface says what the pointer looks like over it

`ICodeSurfaceModel.CursorAt` SHALL answer the pointer's look at a position in the surface's coordinates, and
both realizers SHALL show it as the pointer moves over the surface: the text cursor over the code, and the hand
over a link.

#### Scenario: The hand over a link

- **WHEN** ⌘ is held over a word whose definition a provider answers
- **THEN** `CursorAt` answers the hand there, the web surface's cursor is `pointer`, and Photon shows the
  pointing hand
