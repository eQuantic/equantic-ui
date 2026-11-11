# layout Specification

## Purpose
How a node takes its place in the box its parent gives it, its line box, its size and its position,
the same on the web and on Photon, so one tree lays out alike on every target.

## Requirements

### Requirement: A Text has its own line box on every target

A `Text` SHALL be laid out on its own line box, at the line height its type style says, whatever its
parent is. On the web it SHALL lower as a block (the multi-line clamp keeping the box the clamp
needs), so a block parent's strut never reaches it.

#### Scenario: A small label in a padded pill

- **WHEN** a Box with padding 6 by 2 and a 1px border holds a Text at `TypeStyle(10, 15, …)`
- **THEN** the Box is 21px tall in Chromium and the Text 15px, at 3px from the box's top, the same as
  through a Column

#### Scenario: A Text that is a flex item

- **WHEN** a Text sits directly in a Row
- **THEN** its geometry is what it was, since a flex item was a block already

### Requirement: A drawing at a fill width keeps the artwork's aspect

A `Drawing` whose width is `SizeValue.Fill` and whose height is omitted SHALL be as wide as its
parent offers and as tall as that width divided by the artwork's aspect, on the web (server render
and hydration, with identical markup) and on Photon. An explicit height SHALL win over the aspect.
A drawing at a dp width SHALL lay out exactly as before.

#### Scenario: A filling drawing in a 400dp column

- **WHEN** a `Drawing` of a 480 × 832 artwork with `width: SizeValue.Fill` is laid out in a column
  400dp wide
- **THEN** its box is 400 × 693.33dp on Photon, and the web lowers it to `width: 100%` with
  `aspect-ratio: 0.5769`

#### Scenario: A dp width is unchanged

- **WHEN** a `Drawing` of a 3 : 1 artwork is given `width: 240`
- **THEN** its box is 240 × 80, as before

### Requirement: A positioned child is placed by fractions of its stack and of itself

A `Positioned` child SHALL be placed at its point offsets plus its fraction offsets times the
stack's size on that axis, and then moved by its shift times its own size, on both targets. With
neither a start nor an end (or a top nor a bottom) set, the axis SHALL keep the stack's alignment.

#### Scenario: A tooltip centred above a point

- **WHEN** a 100 × 30 child is positioned in a 400 × 800 stack with `StartFraction = 0.5`,
  `TopFraction = 0.25`, `Top = -16`, `ShiftX = -0.5` and `ShiftY = -1`
- **THEN** Photon places it at x = 150, y = 154, and the web lowers the anchor to
  `left: 50%`, `top: calc(25% - 16px)` and `transform: translate(-50%, -100%)`

#### Scenario: An end fraction

- **WHEN** a 40 × 20 child is positioned in a 400 × 800 stack with `EndFraction = 0.1`
- **THEN** Photon places its right edge 40dp from the stack's right edge (x = 320)

### Requirement: An auto-fill track repeats as often as fits

A grid whose columns are one `GridTrack.AutoFill(min, weight)` SHALL have as many columns as fit
tracks of at least `min` with the grid's gap between them (at least one), and SHALL share the
remaining width between them. A grid that combines an auto-fill track with any other track SHALL be
refused at construction.

#### Scenario: Cards in an 820dp panel

- **WHEN** a grid of `GridTrack.AutoFill(210)` with a 10dp gap is laid out 820dp wide
- **THEN** it has 3 columns of 266.67dp on Photon, and the web lowers it to
  `grid-template-columns: repeat(auto-fill, minmax(min(210px, 100%), 1fr))`

#### Scenario: Narrower than one track

- **WHEN** the same grid is laid out 150dp wide
- **THEN** it has one column 150dp wide

#### Scenario: Combined with another track

- **WHEN** a grid is constructed with `[GridTrack.AutoFill(210), GridTrack.Fixed(40)]`
- **THEN** construction throws, naming the auto-fill track as one that must stand alone

### Requirement: A Text takes the width its parent decides

A `Text` SHALL take the width its parent decides, where the parent decides one: a `Box` with a width,
a `Row` or a `Column` that stretches its children on that axis, and the page itself. Its lines SHALL
align across that width on every target. Its height SHALL stay the height of its lines inside a Box,
and a block stretch SHALL stop at an inline boundary, where the text keeps its content's width.

#### Scenario: A centred line in a sized Box

- **WHEN** a `Box` 300 wide holds a single-line `Text` with `Align = TextAlignment.Center`
- **THEN** the line is drawn centred across the 300, on Photon as on the web

#### Scenario: A centred paragraph in a stretching Column

- **WHEN** a `Column` 380 wide with `Cross = CrossAlign.Stretch` holds a two-line centred `Text`
- **THEN** both lines centre across the 380, the longest one included

#### Scenario: A button's label in a sized Box

- **WHEN** a `Box` 300 wide holds a `Pressable` whose child is a centred `Text`
- **THEN** the button hugs its label, as a `button` does inside a `div`

### Requirement: A stack caps a layer only where Photon does

On the web, a `Stack`'s layer SHALL be capped by the stack only on an axis Photon measures it against
the stack's extent: an axis the layer's child fills, an axis a `ScrollView` hugs, and either axis of a
layer that holds a scroller further down. On any other axis
the layer's size SHALL count toward the stack's own minimum, on the server and in the twin alike, so a
line cannot squeeze a hugging stack below its content.

#### Scenario: A hugging stack beside a wider sibling

- **WHEN** a row 726 wide holds a hugging `Stack` around a box 44 wide, or a hugging column of them, and a box 768 wide
- **THEN** the stack or the column is 44 wide in Chrome, as on Photon

#### Scenario: A filling scroller

- **WHEN** a `Stack` that fills a box 482 wide holds a horizontal `ScrollView` whose width is Fill, around 4000 of content
- **THEN** the stack is 482 wide and its cell is written `min-width: 0` and `min-height: 0`

#### Scenario: A hugging scroller

- **WHEN** the same stack holds a horizontal `ScrollView` with no width of its own
- **THEN** the stack is 482 wide and its cell is written `min-width: 0` and `min-height: 0`

#### Scenario: A layer that holds a scroller

- **WHEN** a `Stack` that fills a box 482 wide holds a hugging `Column` around a horizontal `ScrollView` over 4000 of content
- **THEN** the stack is 482 wide and the cell is written `min-width: 0` and `min-height: 0`

#### Scenario: A fixed layer that holds a scroller

- **WHEN** a `Stack` holds a box 44 wide around a column that holds a horizontal `ScrollView`
- **THEN** its cell is written `min-height: 0` and no `min-width`, the box's own width bounding the track

#### Scenario: A layer of a fixed size

- **WHEN** a `Stack` holds a box 44 wide and 24 tall, or a `ScrollView` 300 wide and 20 tall
- **THEN** its cell is written neither `min-width` nor `min-height`

#### Scenario: A layer that fills one axis

- **WHEN** a `Stack` holds a box whose width is Fill and whose height is 20
- **THEN** its cell is written `min-width: 0` and no `min-height`

### Requirement: A Positioned's anchor carries its key, bookmark and origin

The anchor a `Stack` builds for a `Positioned` SHALL carry the Positioned's key, its bookmark as the
element's `id` with the room a bookmark keeps above itself, and its origin, as the funnel writes them
on every other node's element, on the server and in the twin alike.

#### Scenario: A bookmarked Positioned

- **WHEN** a `Stack` holds `new Positioned(child, top: 0, end: -28) { Bookmark = "request", Key = "request-card", Origin = "Landing.cs:42" }`
- **THEN** its anchor has the id `request`, the key `request-card`, the attribute `data-eq-origin="Landing.cs:42"` and a `scroll-margin-top`, on both sides of hydration

#### Scenario: A component the stack expands

- **WHEN** a `Stack` holds a component that builds a `Positioned`, or one that builds a box, each with a `Bookmark`
- **THEN** the anchor, or the box's element, has that id, on both sides of hydration

### Requirement: A positioned child that fills spans the stack its flow sized

A `Stack` SHALL be sized by its flow children before its positioned children are measured. A positioned child that fills an axis it is anchored on SHALL span that axis between its anchors, on both targets, also when the stack sizes from its content. The axis it spans SHALL be decided by the stack, whatever the stack's own parent left open.

#### Scenario: An action behind a row

- **WHEN** a `Stack` that fills a 400dp width holds `Positioned(Box { Width = 96, Height = Fill }, top: 0, bottom: 0, end: 0)` and a box 78dp tall in its flow
- **THEN** the stack is 78dp tall, and Photon places the action at x = 304, 96 x 78, as the web's absolute box spans its grid

#### Scenario: Through a component

- **WHEN** the positioned child is built by a component
- **THEN** it spans the stack's height in the same way

#### Scenario: A swipeable row

- **WHEN** a `SwipeableRow` holds a row 78dp tall, under a box that hugs its height
- **THEN** its action is 78dp tall on Photon, as on the web

### Requirement: A SafeArea takes its child's size on every target

A `SafeArea` SHALL take its child's size on each axis, with the insets inside the box it takes. On the web, a child that lowers to `width: 100%` or `height: 100%` SHALL make the area take the same, with `box-sizing: border-box` and the child's `max-width`, also when the child is a component. Around an `AdaptiveNode`, the area SHALL fill an axis that every arm fills.

#### Scenario: A page that fills, beside an overlay

- **WHEN** a `Stack` holds a `SafeArea` around a column that fills, and an `Overlay`, in a window 1100 wide
- **THEN** the page is 1100 wide on the web, as on Photon

#### Scenario: A rail built by a component

- **WHEN** a `Row` that fills a window 720 tall holds a `SafeArea` around a `NavigationRail`, all inside a `SafeArea` around an `AdaptiveNode` whose arms both fill
- **THEN** the rail is 720 tall on the web, from the top, as on Photon

#### Scenario: An arm that hugs

- **WHEN** one arm of an `AdaptiveNode` in a `SafeArea` fills the height and the other hugs it
- **THEN** the area does not take `height: 100%`

#### Scenario: A child that hugs

- **WHEN** a `SafeArea` holds a `Text`
- **THEN** the area is written with no width, height or box sizing of its own

### Requirement: Every layout-transparent wrapper passes its child's fill through

Every reader that decides whether a node fills (`Fills` on the web, its TypeScript twin and `FillsOf` on Photon) SHALL look through every wrapper `IsLayoutTransparent` names, and through no other.

#### Scenario: Every wrapper the vocabulary declares

- **WHEN** each layout-transparent wrapper, around a box that fills its height, is positioned at `top: 30` in a stack 90dp tall
- **THEN** Photon measures it 60dp tall

#### Scenario: The browser's list

- **WHEN** the vocabulary declares a layout-transparent wrapper
- **THEN** the TypeScript `fills` has a case for its node kind, or a test fails naming it
