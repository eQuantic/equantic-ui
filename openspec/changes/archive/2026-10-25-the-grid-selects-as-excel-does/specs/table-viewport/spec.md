## ADDED Requirements

### Requirement: A mark may glide to where it moves

A mark that names a key and a glide SHALL, when a later build moves the mark of that key to another
range, be drawn moving from the rectangle it stood over to the new one, over the glide's duration and
along its curve, on every target, laid out at each rectangle on its way rather than scaled to it. A mark
with no glide, a mark of a key the previous build did not hold, and every mark under the platform's
reduced-motion setting SHALL be drawn where it stands at once, and a glide turned on or off SHALL leave
the mark where it stands. A gliding mark SHALL be clipped and hidden by the pinned bands as every mark is.

#### Scenario: A mark that moves a column

- **WHEN** a mark keyed `cursor` with a glide of 200 milliseconds covers cell (2, 2), and the next build
  places it over cell (2, 3)
- **THEN** 100 milliseconds later the mark is drawn halfway between the two cells, and 200 milliseconds
  later over cell (2, 3), on the web as on Photon

#### Scenario: A mark that grows

- **WHEN** a gliding mark keyed `cursor` with a 2-point outline covers one column, and the next build
  places it over three
- **THEN** halfway through the glide it is drawn two columns wide with its outline still 2 points thick,
  and what it holds keeps its own size

#### Scenario: A glide turned on in a scrolled grid

- **WHEN** the grid is scrolled a column across and a mark keyed `cursor` with no glide gains one over
  the same cell
- **THEN** the mark is drawn over that cell, and nothing glides

#### Scenario: A mark at a far row

- **WHEN** the grid of 28-point rows is scrolled 28 000 001 points down and a gliding mark covers the
  cell of row 1 000 001
- **THEN** the mark is drawn over the very pixels of that cell, on the web as on Photon

#### Scenario: Reduced motion

- **WHEN** the platform asks for reduced motion and the same mark moves from (2, 2) to (2, 3)
- **THEN** the mark is drawn over (2, 3) on the first frame

#### Scenario: A new mark

- **WHEN** a build adds a mark keyed `cursor` that the previous build did not hold
- **THEN** it is drawn over its range at once
