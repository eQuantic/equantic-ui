## Why

Closes #658 and #700, both sub-issues of #194 (pointer, gestures and focus). It rides in the pull
request that made a transformed surface take the pointer where it is drawn (#690), whose first review
round asked for what it does: a canvas heard the pointer at the corner of its drawn box, and a turned
completion list took the presses in the corners of the box around it.

Since #513 a surface drawn under a transform takes the pointer where it is drawn. What a press MEANS
inside it still went through the box it is drawn in, and so did what the host places by a caret:

- #658: the host turned a press into a caret, a code position and a cell by subtracting the corner
  of that box: exact for a translation, twice the column for a field drawn twice as large, and the
  wrong axis for a turned sheet. A canvas heard the pointer the same way, and a sheet's fill handle
  was grabbed where it would be drawn unscaled.
- #700: a reveal, the scroll that brings a control Tab reached or a caret the keyboard moved into
  view, measured on screen and scrolled by that distance, while a scroll offset is a distance in the
  scroll view's own space. The anchor of the platform's candidate window was the caret's unscaled
  offset from the box's corner.

## What Changes

For a developer, a field, a code editor, a spreadsheet and a canvas drawn under a
`BoxStyle.Transform` (a scale-in, a hover lift, a turned card) behave as they do unscaled, with no
line of their code changed:

- A press, a drag and a release put the caret, the selection, the cell and the canvas's coordinates
  under the pointer, and the fill handle is grabbed where it is drawn.
- What a code editor offers at its caret takes the pointer in its own shape, and the code drawn in the
  corners of the box around a turned list keeps its presses.
- The input method's candidate window opens by the caret as drawn.
- A scroll view drawn scaled reveals a focused control and a caret as far as it would unscaled.

The parts it reaches: the Photon host and its input sink (`eQuantic.UI.Native.Components`). Nothing
in eqc, the runtime or the web realizer moves, since the browser already does all of it. Neither the
public surface nor the developer surface moves.

## Capabilities

### New Capabilities

- `transformed-surfaces`: what a press means on a surface drawn under a transform, where the host
  places what stands by its caret, and how far a scroll view drawn under one scrolls.

### Modified Capabilities

None.

## Impact

Photon only, and only under a transform: a frame that transforms nothing takes the arithmetic it
always did. Nothing is drawn differently, so no golden moves.
