# code-editor Specification

## Purpose
The write-once `CodeEditor` as a person presses it: where a press on its code puts the caret, and
which presses are not its code's, the same on the web and on Photon.

## Requirements

### Requirement: A press under a bounded editor's code is the end of the document

A bounded `CodeEditor` (one that fills its place or has a fixed height) SHALL draw its code as tall as
its viewport however short the file. A press under its last line SHALL put the caret at the end of the
document and give the editor the keyboard, and a drag from there SHALL select back to where it stops.

#### Scenario: Under a two-line file

- **WHEN** an editor 400 tall holding `one` and `two three` is pressed 300 below its top, under the
  second column of its last line
- **THEN** the caret is at the end of `two three`, and the editor has the keyboard

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
