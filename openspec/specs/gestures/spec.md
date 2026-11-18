# gestures Specification

## Purpose
What a continuous gesture does to the subtree it moves: where it rests, how it composes with what
the subtree draws, and where it ends.

## Requirements

### Requirement: A draggable's offset composes with its box's transform

A following `Draggable`'s resting offset and the offset of a gesture in progress SHALL compose with the
transform its child draws, its resting one and its states', as Photon composes them. On the web the
offset SHALL ride the individual `translate` property, on both producers and in the drag controller,
and its glide SHALL join the child's own transition list.

#### Scenario: An open row under the pointer

- **WHEN** a draggable rests at -80 over a box with `Transform = Scale(0.9)` and a hover that
  translates it up, and the pointer rests on it
- **THEN** Chromium computes `translate: -80px` with the hover's `transform`, so the row stays open

#### Scenario: A swipe that changes nothing

- **WHEN** a row is dragged and released without its `RestOffset` changing
- **THEN** the controller hands the surface back to its markup and it glides to its rest, where it
  used to stay where the finger left it

### Requirement: A normalized drag on the web measures against the box its rest is written in

On the web, a normalized `Draggable` SHALL divide the finger's travel, read in the space its offset
moves in, by the surface's untransformed box, the one a `translate` percentage resolves against, and
SHALL write its value back in that same box, so the surface stays under the finger from its rest. No
transform, the surface's own or an ancestor's, SHALL enter the division.

#### Scenario: A surface with a scale of its own

- **WHEN** a normalized `Draggable` that follows, 200 wide with a `scale(.5)` and a rest of 0.25, is dragged 14px and then 40px
- **THEN** in Chrome it moves 14px and then 40px from where it rested, with no jump, and a release reports 0.45

#### Scenario: A surface turned by its own transform

- **WHEN** a normalized horizontal `Draggable` 200 wide and 40 thick, turned 90° by its own transform, is dragged 40px sideways
- **THEN** in Chromium its offset is 40px, drawn 40px away, and a release reports 0.2, where it jumped to 1

#### Scenario: A surface under a turned ancestor

- **WHEN** the same `Draggable`, unturned, sits in a box turned 90° and is dragged 40px down the screen
- **THEN** in Chromium its offset is 40px, drawn 40px down the screen, and a release reports 0.2

### Requirement: A drag on the web leaves no text selected

Every pointer gesture on the web's gesture core (a sortable list, a draggable, a sheet dismissed by dragging)
SHALL clear the text selection its press started once the gesture arms, and SHALL keep any selection
from starting or growing until the gesture ends, when the page's own `user-select` SHALL be given back. A
press that never becomes a gesture SHALL leave the selection to the browser.

#### Scenario: A sortable item made of text

- **WHEN** a mouse drags a table's heading, a text with no press of its own, past another heading
- **THEN** the column moves, and no text of the page is selected

#### Scenario: A press that does not travel

- **WHEN** a mouse presses on text in a sortable item and releases it without travelling past the slop
- **THEN** whatever the browser selected stays selected

#### Scenario: Text outside any gesture

- **WHEN** a mouse drags across ordinary text after a gesture has ended
- **THEN** the text is selected as the browser selects it
