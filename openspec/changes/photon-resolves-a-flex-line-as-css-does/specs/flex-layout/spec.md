## ADDED Requirements

### Requirement: A weighted item starts from its basis

A `Flexible` whose weight is above zero SHALL start from its basis, or from nothing when it declares
none, and SHALL grow by its share of the leftover on top of that start, on the web and on Photon, as
a browser resolves `flex: <weight> <shrink> <basis>`. Weights that sum to less than one SHALL share
only that fraction of the leftover, as Flexbox §9.7 does; only an animated weight sums that low. The
start SHALL follow the weight the app declared and the share the weight an animation resolves, so a
weight animated to zero at a basis glides down to the basis and lands there.

#### Scenario: A basis beside a weight of the same size

- **WHEN** a row 1440 wide holds `Flexible(pane, flex: 1, basis: 540)` and `Flexible(pane, flex: 1)`
- **THEN** the first item is 990 wide and the second 450, on Photon and in Chrome

#### Scenario: Weights share what is left over their bases

- **WHEN** a row 1000 wide holds `Flexible(pane, flex: 2, basis: 100)`, `Flexible(pane, flex: 1, basis: 200)` and a box fixed at 100
- **THEN** the items are 500, 400 and 100 wide, where sharing the whole row by weight gives 600 and 300

#### Scenario: A weighted basis in an overflowing line

- **WHEN** a row 400 wide holds a box fixed at 100 and `Flexible(pane, flex: 1, basis: 540)`, or holds `Flexible(pane, flex: 1, basis: 540, shrink: 0)` alone
- **THEN** the weighted item is 300 wide, and with `shrink: 0` it keeps all 540

#### Scenario: A weight animated to zero

- **WHEN** a row 1440 wide holds `Flexible(pane, flex: 1, basis: 540) { AnimateChanges = true }` and its weight changes to 0
- **THEN** the item glides from 1440 down to 540 over the motion's duration, never growing on the way and with no frame that jumps, and lands on 540

### Requirement: A wrapping line breaks a weighted item without a basis on nothing

A wrapping line SHALL break a weighted `Flexible` that declares no basis against a hypothetical size
of zero, as CSS starts it from `0%`, on the web and on Photon, and SHALL count the gap between two
items whatever size they ask for.

#### Scenario: Two weights around content wider than half the line

- **WHEN** a wrapping row 1000 wide holds two `Flexible(box 600 wide, flex: 1)` without a basis
- **THEN** both sit on one line, at x 0 and 500, each 500 wide, on Photon and in Chrome

#### Scenario: With a gap

- **WHEN** the same row has a gap of 10
- **THEN** both sit on one line, each 495 wide, the second at x 505

### Requirement: A line that stretches keeps a declared cross size

A flex line that stretches its children on the cross axis SHALL keep a cross size a child declares,
fixed or window-relative, as CSS's stretch fills only an auto cross size: read by the same classifier
the main axis reads, of the node the child measured to, which is what a component built.

#### Scenario: Every node type that declares a height and draws

- **WHEN** a row 400 wide and 100 tall that stretches holds a `Box`, a `Stack`, a `Canvas`, a `CameraPreview` or a `ScrollView` 20 tall
- **THEN** each is 20 tall on Photon, as in Chrome

#### Scenario: A web frame on Photon

- **WHEN** the same row holds a `WebFrame` 20 tall
- **THEN** it is not stretched to the row's 100 on Photon, where a web frame measures as nothing

#### Scenario: What a component builds

- **WHEN** the same row holds a component that builds a box 20 tall
- **THEN** the component's item is 20 tall on Photon

## MODIFIED Requirements

### Requirement: A zero weight gives space back as the web lets it

In a line that overflows, a `Flexible` of weight zero SHALL give space back on Photon as the web's
`flex: 0 <shrink> <basis>; min-width: 0` lets it in a browser (`min-height: 0` in a column): as far
as nothing, whatever the min-content of what it holds; in proportion to its shrink times its size;
and together with the other items that shrink, whether it holds text or not. A share that would
carry it below nothing SHALL stop there, and what it could not give SHALL be shared among the
others. In a single-line column the other items SHALL keep the height they measured, which is their
min-content height, and text SHALL NOT be cut.

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
- **THEN** the zero weight is 300 tall on Photon, as in Chrome, and the column's content ends at 400

#### Scenario: A column beside a hugging item

- **WHEN** a column 400 tall holds a box that hugs a box 100 tall, and `Flexible(box, flex: 0, basis: 540)`
- **THEN** the hugging box keeps its 100 and the zero weight is 300 tall

### Requirement: A Flexible's slot sizes the item, and a fixed child keeps its own size

A `Flexible` SHALL take its item's size on Photon, in a single line (its share, its basis, or what an
overflowing line left it) and on a wrapping line (the size its line resolved, or its basis when the
line neither grows nor shrinks), and its child SHALL keep a main size its node declares, wider than
the item or narrower, as a browser keeps such a child inside its flex item and lets it overflow: a
fixed or window-relative `SizeValue` it carries (a box's style, a flex container, a grid, a stack, a
scroller, a canvas, a web frame) or a size its constructor demands (an image, an icon, a vector, a
drawing, a spinner, a camera preview), read of the node the child measured to: through transparent
wrappers, through a component to what it built, and through an `AdaptiveNode` to the arm laid out in
its place. A `ScrollView` SHALL keep
its declared width only up to the item, as the web's `max-width: 100%` caps it, and SHALL be capped
itself, through any transparent wrapper and as an arm, so it paints, clips and scrolls at that width,
its scroll range taken at that width on a single line and on a wrapping one. A child that
declares no main size SHALL fill the item: an auto or Fill box, and a `Text`, whose line box the item
is. Every node type of the vocabulary that declares a size SHALL be read as declaring it, on each axis
it declares one.

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
- **THEN** `DeclaredSizeKind` reads each as Fixed on that axis, and a `SizeValue` declared as Fill as Fill, through every node type that takes its one child's size
