## MODIFIED Requirements

### Requirement: A zero weight gives space back as the web lets it

In a ROW that overflows, a `Flexible` of weight zero SHALL give space back on Photon as the web's
`flex: 0 <shrink> <basis>; min-width: 0` lets it in a browser: as far as nothing, whatever the
min-content of what it holds; in proportion to its shrink times its size; and together with the
other items that shrink, whether it holds text or not. A share that would carry it below nothing
SHALL stop there, and what it could not give SHALL be shared among the others. In a single-line
column Photon takes nothing back from an overflowing line yet, from any item, where a browser
shrinks a zero weight the same way.

#### Scenario: Past its child's min-content

- **WHEN** a row 400 wide holds a box fixed at 100 and `Flexible(box 400 wide, flex: 0, basis: 540)`, or the same zero weight without a basis
- **THEN** the zero weight is 300 wide on Photon, as in Chrome, and the line ends at the row's end

#### Scenario: By its shrink times its size

- **WHEN** a row 1000 wide holds `Flexible(pane, flex: 0, basis: 540, shrink: 3)` and `Flexible(pane, flex: 0, basis: 540, shrink: 1)`
- **THEN** they are 480 and 520 wide

#### Scenario: Beside a box that fills the row

- **WHEN** a row 1000 wide holds `Flexible(box 400 wide, flex: 0, basis: 540, shrink: 3)` and a box whose width is Fill
- **THEN** they are 206.11 and 793.89 wide

#### Scenario: Holding text

- **WHEN** a row 400 wide holds `Flexible(Text("aaaa"), flex: 0, basis: 300)` and `Flexible(box 300 wide, flex: 0, basis: 300)`, and a row 300 wide holds `Flexible(Text("aaaa"), flex: 0, basis: 300, shrink: 3)` and `Flexible(Text("bbbb"), flex: 0, basis: 200, shrink: 1)`
- **THEN** the first two are 200 and 200 wide, and the second two 136.36 and 163.64

#### Scenario: A column on Photon

- **WHEN** a column 400 tall holds a box fixed at 100 and `Flexible(box, flex: 0, basis: 540)`
- **THEN** the zero weight is 300 tall in Chrome and stays 540 on Photon, whose single-line column takes nothing back yet

### Requirement: A Flexible's slot sizes the item, and a fixed child keeps its own size

A `Flexible` SHALL take its item's size on Photon, in a single line (its share, its basis, or what an
overflowing line left it) and on a wrapping line (the size its line resolved, or its basis when the
line neither grows nor shrinks), and its child SHALL keep a main size its node declares, wider than
the item or narrower, as a browser keeps such a child inside its flex item and lets it overflow: a
fixed or window-relative `SizeValue` it carries (a box's style, a flex container, a grid, a stack, a
scroller, a canvas, a web frame) or a size its constructor demands (an image, an icon, a vector, a
drawing, a spinner, a camera preview), seen through transparent wrappers. A `ScrollView` SHALL keep
its declared width only up to the item, as the web's `max-width: 100%` caps it, and SHALL be capped
itself, through any transparent wrapper, so it paints, clips and scrolls at that width. A child that
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

#### Scenario: A wrapping line that holds still

- **WHEN** a wrapping row 540 wide holds `Flexible(box 400 wide, flex: 0, basis: 540)`, `Flexible(box 600 wide, flex: 0, basis: 540)` or `Flexible(box 400 wide, flex: 1, basis: 540)`, and a wrapping row 1000 wide holds `Flexible(box 400 wide, flex: 0, basis: 540)` and `Flexible(box 100 wide, flex: 0, basis: 200)`
- **THEN** each item in the row of 540 is 540 wide with its box at its own width, and in the row of 1000 the second item starts at 540

#### Scenario: Every node type that declares a size

- **WHEN** the vocabulary's node types are taken from the Primitives assembly, and each one that declares a width or a height is given a fixed one
- **THEN** `MainSizeKind` reads each as Fixed, and a `SizeValue` declared as Fill as Fill
