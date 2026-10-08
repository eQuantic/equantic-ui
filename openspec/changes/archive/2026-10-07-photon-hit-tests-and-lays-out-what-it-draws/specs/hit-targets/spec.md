## ADDED Requirements

### Requirement: A press inside a control's own box is that control's

A point inside the box a control is drawn in SHALL be that control's, before any neighbour's slop.
A slop SHALL take a point only where no box beside it is drawn, or in front of the box it stands in,
for a press, a tap and the pointer's shape alike. A control whose box its clips leave nothing of
SHALL stand in front of no box: its slop takes a point only where no box is drawn.

#### Scenario: Rows shorter than the minimum target

- **WHEN** a column holds three 20dp pressable rows, under a finger or a pointer, and a row is pressed
  at its top, its middle and its bottom
- **THEN** each press runs that row, and a press 5dp above the first row or under the last runs that
  row

#### Scenario: A disabled neighbour

- **WHEN** an enabled row stands above a disabled one under a finger, and the enabled row is pressed
  half a point above its bottom
- **THEN** the enabled row runs, and the pointer there is a pointer

#### Scenario: A control inside a card

- **WHEN** a 20dp pressable stands inside a pressable card, and the card is pressed 6dp beside the
  control and at its own corner
- **THEN** the first press runs the control and the second runs the card

#### Scenario: A row its scroll view clipped away

- **WHEN** a scroll view 100dp tall shows five 20dp rows under a finger, the sixth starting on its
  bottom edge, and the fifth row is pressed 5dp above its bottom
- **THEN** the fifth row runs, and the sixth does not

#### Scenario: Rows turned together

- **WHEN** a 200dp row and a 20dp row centred under it are turned 45° together under a finger, and the
  wide row is pressed 2dp above its foot, where the narrow row's slop reaches
- **THEN** the wide row runs, since the narrow one is drawn beside it

### Requirement: A transformed box takes the pointer where it is drawn

Every region a box registers under a transform, its own and its subtree's, SHALL take the pointer
where it is drawn: a press, a hover, the pointer's shape, a drag and a scroll. A rotated region SHALL
be tested against its shape. A pressable that wraps a single box SHALL take the pointer where that box
is drawn. A transform that collapses a box onto a line or a point SHALL leave it nowhere to take the
pointer, since nothing of it is drawn.

#### Scenario: A translated box

- **WHEN** a 40dp pressable square that fills on hover is laid out at the top and drawn 20dp lower
- **THEN** a tap and a hover 50dp down press it and hover it, and 10dp down do neither

#### Scenario: A scaled box

- **WHEN** a 40dp square laid out from 30 to 70 is drawn twice as large about its centre
- **THEN** a tap at 15 and at 85 presses it

#### Scenario: A rotated box

- **WHEN** a 40dp square laid out from 30 to 70 is turned 45° about its centre
- **THEN** a tap 27dp above its centre presses it, and a tap at the corner of the box around it does not

#### Scenario: A box collapsed onto a line

- **WHEN** a 40dp pressable square that fills on hover is squashed to no width and turned 45° about its
  centre
- **THEN** a tap on the line it collapsed onto, and one beside it, press nothing, and the pointer beside
  it hovers nothing

### Requirement: An editing surface takes presses only where it is on screen

A text field and a spreadsheet SHALL take a press only on the part of them that the scroll views
around them leave on screen, while their whole bounds still place the caret and the cells.

#### Scenario: A field past its scroll view

- **WHEN** a text field runs past the bottom of the scroll view showing it, over a box that takes no
  press, and the box is pressed where the field runs under it
- **THEN** nothing takes the keyboard, and a press on the field's visible part does

#### Scenario: A sheet past its scroll view

- **WHEN** a spreadsheet runs past the bottom of the scroll view showing it, and the box below the view
  is pressed where the sheet runs under it
- **THEN** the sheet takes no press
