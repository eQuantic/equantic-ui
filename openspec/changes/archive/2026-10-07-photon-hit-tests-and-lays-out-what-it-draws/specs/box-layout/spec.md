## ADDED Requirements

### Requirement: A box keeps its child inside its border

A box SHALL lay its child inside its padding and its border, on every side the border is drawn on, on
both targets, and a box that hugs its child SHALL count both in its size.

#### Scenario: A border on every side

- **WHEN** a box with a 4dp padding and a 2dp border hugs a 10dp square
- **THEN** the square stands 6dp in from the box's top and start, and the box is 22dp square

#### Scenario: A border on one side

- **WHEN** a box with a 4dp padding and a 3dp border drawn on its start alone hugs a 10dp square
- **THEN** the square stands 7dp in from the start and 4dp from the top, and the box is 21 by 18
