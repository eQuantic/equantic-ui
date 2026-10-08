## ADDED Requirements

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
