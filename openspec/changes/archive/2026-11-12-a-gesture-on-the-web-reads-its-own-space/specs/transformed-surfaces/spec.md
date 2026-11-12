## MODIFIED Requirements

### Requirement: A captured gesture is read in the surface's own space

On Photon, a gesture captured on a surface drawn under a transform SHALL read every point from press
to release in the space the surface was laid out in, through the transform the press found, held for
the whole gesture: a drag's travel, its normalized value, its paint offset and its release, a dismiss's
threshold, and a pan's positions, scroll offset and fling speed.

On the web, a drag and a dismiss SHALL read every point from press to release in the space their
offset moves in, the surface's parent's, through the inverse of the transforms of the surface's
ancestors as the press finds them, held for the whole gesture: the travel, the value reported, the
offset written and a dismiss's threshold. The surface's own transform SHALL stay out of that space, as
Photon paints a drag's offset outside its child's transform, and the slop SHALL stay a distance on the
screen.

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

#### Scenario: A surface turned by its own transform

- **WHEN** a horizontal `Draggable` whose child is turned 90° by its own transform is dragged 40 sideways on Photon
- **THEN** its paint offset is 40, along the axis around the child, and a normalized one over a child 200 wide reports 0.2

#### Scenario: A drag on the web under a turned ancestor

- **WHEN** a horizontal `Draggable` inside a box turned 90° is dragged 40px down the screen in Chromium
- **THEN** its `translate` is 40px, it is drawn 40px down the screen, and a release reports 40, where it heard nothing

#### Scenario: A drag on the web under a scaled parent

- **WHEN** a horizontal `Draggable` inside a parent at `scale(2)` is dragged 40px sideways in Chromium
- **THEN** its `translate` is 20px, it is drawn 40px away, under the finger, and a release reports 20, where it was drawn 80 away and reported 40

#### Scenario: The slop on the web stays the finger's

- **WHEN** the same `Draggable` under `scale(2)` is moved 8px on the screen, and then 16px
- **THEN** 8 arms nothing, and 16 arms it with a `translate` of 8px

#### Scenario: A dismiss on the web under a scaled parent

- **WHEN** a `DragDismiss` with a threshold of 96 inside a parent at `scale(2)` is dragged 120px down the screen in Chromium and released
- **THEN** it is drawn 120px away, under the finger, and stays, having travelled 60 of its own, where it was drawn 240 away and dismissed
