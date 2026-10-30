## ADDED Requirements

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
