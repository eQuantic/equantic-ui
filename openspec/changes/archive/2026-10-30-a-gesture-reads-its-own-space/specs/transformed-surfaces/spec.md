## ADDED Requirements

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

#### Scenario: A dismiss drawn twice as large

- **WHEN** a `DragDismiss` is drawn at a scale of 2 and released just past its threshold on the screen
- **THEN** it does not dismiss, having travelled half of that in its own space

#### Scenario: A pan

- **WHEN** a vertical `ScrollView` drawn at a scale of 2 is panned 40 on the screen, or one turned 90° is panned 40 sideways
- **THEN** its offset is 20, or 40

#### Scenario: A flick

- **WHEN** the same gesture in the view's own space is flicked at a scale of 1 and of 2
- **THEN** both land at the same offset
