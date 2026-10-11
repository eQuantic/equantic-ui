## MODIFIED Requirements

### Requirement: A Flexible's slot sizes the item, and a fixed child keeps its own size

A `Flexible` in a single line SHALL take its slot on Photon (its share, its basis, or what an
overflowing line left it), and its child SHALL keep a main size its node declares, wider than the
slot or narrower, as a browser keeps such a child inside its flex item and lets it overflow: a fixed
or window-relative `SizeValue` it carries (a box's style, a flex container, a grid, a stack, a scroller,
a canvas, a web frame) or a size its constructor demands (an image, an icon, a vector, a drawing, a
spinner, a camera preview), seen through transparent wrappers. A `ScrollView` SHALL keep its declared
width only up to the slot, as the web's `max-width: 100%` caps it. A child that declares no main size
SHALL fill the slot: an auto or Fill box, and a `Text`, whose line box the slot is. Every node type of
the vocabulary that declares a main size SHALL be read as declaring it.

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

#### Scenario: Every node type that declares a size

- **WHEN** the vocabulary's node types are taken from the Primitives assembly, and each one that declares a width or a height is given a fixed one
- **THEN** `MainSizeKind` reads each as Fixed, and a `SizeValue` declared as Fill as Fill
