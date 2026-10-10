## MODIFIED Requirements

### Requirement: A Flexible's slot sizes the item, and a fixed child keeps its own size

A `Flexible` SHALL take its item's size on Photon, in a single line (its share, its basis, or what an
overflowing line left it) and on a wrapping line (the size its line resolved, or its basis when the
line neither grows nor shrinks), and its child SHALL keep a main size its node declares, wider than
the item or narrower, as a browser keeps such a child inside its flex item and lets it overflow: a
fixed or window-relative `SizeValue` it carries (a box's style, a flex container, a grid, a stack, a
scroller, a canvas, a web frame) or a size its constructor demands (an image, an icon, a vector, a
drawing, a spinner, a camera preview), read of the node the child measured to: through transparent
wrappers, and through an `AdaptiveNode` to the arm laid out in its place. A `ScrollView` SHALL keep
its declared width only up to the item, as the web's `max-width: 100%` caps it, and SHALL be capped
itself, through any transparent wrapper and as an arm, so it paints, clips and scrolls at that width,
its scroll range taken at that width on a single line and on a wrapping one. A child that
declares no main size SHALL fill the item: an auto or Fill box, and a `Text`, whose line box the item
is. Every node type of the vocabulary that declares a main size SHALL be read as declaring it.

#### Scenario: A fixed child in a zero weight that shrank

- **WHEN** a row 400 wide holds a box fixed at 100 and `Flexible(box 400 wide, flex: 0, basis: 540)`
- **THEN** the item is 300 wide, and the box inside it is 400 wide from the item's start

#### Scenario: A fixed child in a narrower share

- **WHEN** a row 600 wide holds `Flexible(box 400 wide, flex: 1)` and `Flexible(pane, flex: 1)`
- **THEN** the first item is 300 wide and its box 400

#### Scenario: A fixed child in a wider share

- **WHEN** the same row holds `Flexible(box 100 wide, flex: 1)` first
- **THEN** the first item is 300 wide and its box 100

#### Scenario: A child without a size of its own

- **WHEN** the first Flexible holds a box whose width is Fill, or a centred `Text`
- **THEN** the child is 300 wide, the slot

#### Scenario: A camera preview

- **WHEN** the first Flexible holds `CameraPreview(session, 320, 240)`, or a row 400 wide holds a box fixed at 100 and `Flexible(CameraPreview(session, 320, 240), flex: 0, basis: 540)`
- **THEN** the preview is 320 wide in its 300 item

#### Scenario: A scroller's width is a ceiling

- **WHEN** the first Flexible holds a `ScrollView` 400 wide, or one 100 wide
- **THEN** the first is 300 wide, the slot, and the second 100

#### Scenario: The ceiling reaches the scroller

- **WHEN** the first Flexible holds `Pinned(ScrollView { Width = 400 })`, or a horizontal `ScrollView` 400 wide around 800 of content
- **THEN** the wrapper and the scroller are both 300 wide, and the horizontal scroller's range is 500

#### Scenario: A child that declares its size through an arm

- **WHEN** the first Flexible holds an `AdaptiveNode` whose arm is a box 400 wide, or one whose arm is a box 100 wide
- **THEN** the arm is 400 wide in its 300 item, or 100

#### Scenario: A scroller arm

- **WHEN** the first Flexible holds an `AdaptiveNode` whose arm is a horizontal `ScrollView` 400 wide around 800 of content, or one 100 wide
- **THEN** the first arm is 300 wide and scrolls by 500, and the second is 100 wide and scrolls by 700

#### Scenario: A scroller on a wrapping line

- **WHEN** a wrapping row 300 wide holds `Flexible(ScrollView { Width = 400 }, flex: 1, basis: 300)` around 800 of content, or the same with a basis of 200
- **THEN** the scroller is 300 wide and scrolls by 500

#### Scenario: A wrapping line that holds still

- **WHEN** a wrapping row 540 wide holds `Flexible(box 400 wide, flex: 0, basis: 540)`, `Flexible(box 600 wide, flex: 0, basis: 540)` or `Flexible(box 400 wide, flex: 1, basis: 540)`, and a wrapping row 1000 wide holds `Flexible(box 400 wide, flex: 0, basis: 540)` and `Flexible(box 100 wide, flex: 0, basis: 200)`
- **THEN** each item in the row of 540 is 540 wide with its box at its own width, and in the row of 1000 the second item starts at 540

#### Scenario: Every node type that declares a size

- **WHEN** the vocabulary's node types are taken from the Primitives assembly, and each one that declares a width or a height is given a fixed one
- **THEN** `MainSizeKind` reads each as Fixed, and a `SizeValue` declared as Fill as Fill, through every node type that takes its one child's size
