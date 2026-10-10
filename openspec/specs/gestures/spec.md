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
