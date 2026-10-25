## ADDED Requirements

### Requirement: A mark may glide to where it moves

A mark that names a key and a glide SHALL, when a later build moves the mark of that key to another
range, be drawn moving from the rectangle it stood over to the new one, over the glide's duration and
along its curve, on every target. A mark with no glide, a mark of a key the previous build did not hold,
and every mark under the platform's reduced-motion setting SHALL be drawn where it stands at once. A
gliding mark SHALL be clipped and hidden by the pinned bands as every mark is.

#### Scenario: A mark that moves a column

- **WHEN** a mark keyed `cursor` with a glide of 200 milliseconds covers cell (2, 2), and the next build
  places it over cell (2, 3)
- **THEN** 100 milliseconds later the mark is drawn halfway between the two cells, and 200 milliseconds
  later over cell (2, 3), on the web as on Photon

#### Scenario: Reduced motion

- **WHEN** the platform asks for reduced motion and the same mark moves from (2, 2) to (2, 3)
- **THEN** the mark is drawn over (2, 3) on the first frame

#### Scenario: A new mark

- **WHEN** a build adds a mark keyed `cursor` that the previous build did not hold
- **THEN** it is drawn over its range at once
