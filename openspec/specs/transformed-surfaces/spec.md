# transformed-surfaces Specification

## Purpose
What a press means on a surface Photon draws under a transform, where the host places what stands by
its caret, and how far a scroll view drawn under one scrolls: the same as unscaled, read in the
surface's own space.

## Requirements

### Requirement: A press on a transformed surface means what it means unscaled

A press, a drag and a release on a text field, a code surface, a spreadsheet or a canvas drawn under a
transform SHALL be read in the surface's own space: the caret, the selection, the cell, the fill
handle and the canvas's coordinates SHALL be the ones under the pointer as the surface is drawn.

#### Scenario: A field drawn twice as large

- **WHEN** a text field is drawn twice as large about its centre and pressed 50dp from its drawn left
  edge
- **THEN** the caret lands where a press 25dp from the left edge of the same field drawn unscaled
  puts it

#### Scenario: A code surface drawn twice as large

- **WHEN** a code surface drawn twice as large is pressed at the drawn middle of line 1, column 4
- **THEN** the caret is at line 1, column 4

#### Scenario: A sheet turned a quarter

- **WHEN** a square sheet is turned 90° about its centre and pressed where the middle of row 2,
  column 1 is drawn
- **THEN** the selected cell is row 2, column 1

#### Scenario: The fill handle

- **WHEN** a sheet drawn twice as large is pressed at the drawn bottom-right corner of its selected
  cell
- **THEN** a fill drag begins

#### Scenario: A canvas drawn twice as large

- **WHEN** a canvas drawn twice as large is pressed 60 and 80 from its drawn corner
- **THEN** it hears the pointer at 30, 40

### Requirement: What a code surface offers takes the pointer in its own shape

What a code surface offers at its caret SHALL take the pointer where it is drawn, and under a
transform SHALL be tested against its own shape, as the surface is: a point in the box around a turned
list and outside the list SHALL be the code's.

#### Scenario: A turned list

- **WHEN** a code editor turned 30° about its centre shows its completion list, and the pointer stands
  in a corner of the box around the list where the code is drawn
- **THEN** the pointer is the code's beam, and a press there moves the caret

### Requirement: The candidate window opens by the caret as drawn

The anchor the host gives the platform's input method (`PhotonHost.CaretRect`) SHALL be the caret as
it is drawn, under any transform around the field or the code surface.

#### Scenario: A field drawn twice as large

- **WHEN** a field drawn twice as large holds its caret after its third character, each 8dp wide
- **THEN** the anchor stands 48 from the field's drawn left edge, and is 4 wide

### Requirement: A scroll view drawn under a transform reveals as far as it would unscaled

A reveal, the scroll that brings a control Tab reached or a caret the keyboard moved into view, SHALL
measure the control or the caret, the viewport and its margins in the scroll view's own space.

#### Scenario: A control Tab reached

- **WHEN** a scroll view 100dp tall over ten 40dp controls is drawn twice as large, and Tab reaches
  the sixth control
- **THEN** it scrolls exactly as far as the same scroll view drawn unscaled

#### Scenario: A caret the keyboard moved

- **WHEN** a code editor whose view is 100dp tall is drawn twice as large, and its caret goes twenty
  lines down
- **THEN** its view scrolls exactly as far as the same editor's drawn unscaled

#### Scenario: Turned

- **WHEN** the same scroll view and the same editor are turned 45°
- **THEN** each scrolls exactly as far as it does unturned

### Requirement: A captured gesture is read in the surface's own space

On Photon, a gesture captured on a surface drawn under a transform SHALL read every point from press
to release in the space the surface was laid out in, through the transform the press found, held for
the whole gesture: a drag's travel, its normalized value, its paint offset and its release, a dismiss's
threshold, and a pan's positions, scroll offset and fling speed.

#### Scenario: A drag drawn twice as large

- **WHEN** a horizontal `Draggable` is drawn at a scale of 2 and the pointer moves 40 on the screen
- **THEN** its paint offset is 20 of its own space, drawn 40 away, and a release reports 20

#### Scenario: A normalized drag drawn twice as large

- **WHEN** the same `Draggable` is `Normalized`, on a surface 200 wide
- **THEN** a release reports 0.1, and its paint offset is 20

#### Scenario: A drag turned a quarter

- **WHEN** a horizontal `Draggable` is turned 90° and the pointer moves 40 down the screen, along its drawn track
- **THEN** its paint offset is 40, and a release reports 40

#### Scenario: A drag that follows the finger

- **WHEN** the drag drawn twice as large moves on to 80 on the screen
- **THEN** its paint offset is 40, measured against where the gesture started

#### Scenario: The slop stays the finger's

- **WHEN** a drag is drawn at a scale of 2 or of 0.25, and the pointer moves 16 on the screen, or 8
- **THEN** 16 arms it and 8 does not, past the same slop of 12 on the screen

#### Scenario: A dismiss drawn twice as large

- **WHEN** a `DragDismiss` is drawn at a scale of 2 and released just past its threshold on the screen
- **THEN** it does not dismiss, having travelled half of that in its own space

#### Scenario: A pan

- **WHEN** a vertical `ScrollView` drawn at a scale of 2 is panned 40 on the screen, or one turned 90° is panned 40 sideways
- **THEN** its offset is 20, or 40

#### Scenario: A flick

- **WHEN** the same gesture in the view's own space is flicked at a scale of 1 and of 2
- **THEN** both land at the same offset
