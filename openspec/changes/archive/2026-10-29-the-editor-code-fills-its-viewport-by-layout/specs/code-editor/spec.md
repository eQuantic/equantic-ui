## MODIFIED Requirements

### Requirement: A press under a bounded editor's code is the end of the document

A bounded `CodeEditor` (one that fills its place or has a fixed height) SHALL draw its code as tall as
its viewport however short the file, from its first frame. A press under every row its code draws SHALL put
the caret at the end of the document and give the editor the keyboard, and a drag from there SHALL select
back to where it stops. A row after the last line, a diff's filler, SHALL take a press as any filler does: on
the line, at the press's column.

#### Scenario: Under a two-line file

- **WHEN** an editor 400 tall holding `one` and `two three` is pressed 300 below its top, under the
  second column of its last line
- **THEN** the caret is at the end of `two three`, and the editor has the keyboard

#### Scenario: On its first frame

- **WHEN** that editor is pressed 300 below the top of its code after the one frame that first drew it
- **THEN** the caret is at the end of `two three`, and the editor has the keyboard

#### Scenario: A diff's fillers after the last line

- **WHEN** a side of three lines ends with two filler rows, and the second filler is pressed at the
  third column
- **THEN** the caret is on the last line at its third column, and a press below the fillers is at the
  end of the document

#### Scenario: A drag from under the code

- **WHEN** that press is dragged to the second column of the first line and released
- **THEN** the selection runs from the end of the document back to the second column of the first line

## ADDED Requirements

### Requirement: A bounded editor's code never makes its viewport scroll by itself

A bounded `CodeEditor` SHALL scroll vertically only by as much as its code is taller than the room its
viewport shows it in: never by a scrollbar's thickness, never by a height its viewport had before, on the web
and on Photon, with or without its line numbers. A file longer than its viewport SHALL scroll to its last line.

#### Scenario: A short file with a long line, where scrollbars take space

- **WHEN** an editor filling a box 600 tall holds three lines, one of them wider than the editor, in a browser
  whose scrollbars take 17px
- **THEN** the editor does not scroll vertically, and a press under its last line puts the caret on that line
  and gives its input the focus

#### Scenario: A pane that shrinks

- **WHEN** an editor 400 tall holding two lines is made 300 tall
- **THEN** on the next frame its code is 300 tall and it has nothing to scroll

#### Scenario: No line numbers, a long file

- **WHEN** an editor 300 tall with no line numbers holds 60 lines
- **THEN** it scrolls to its last line
