## ADDED Requirements

### Requirement: A delegate lays out a node's children

A `CustomMultiChildLayout` SHALL lay out its children, each a `LayoutId`, with its `MultiChildLayoutDelegate`, on
Photon and in the browser: `LayoutChild(id, constraints)` SHALL measure the child with that id as the content of a
box with those bounds and answer its size, and `PositionChild(id, offset)` SHALL place it at that offset from the
node's origin. The default `Layout` SHALL ask `GetSize` first and run `PerformLayout` with the size constrained to
the room; a delegate that overrides `Layout` SHALL answer the node's size after laying its children out.

#### Scenario: A radial menu

- **WHEN** a delegate's `GetSize` answers 240 by 240 and its `PerformLayout` places six 48 by 48 children on a
  circle of radius 96 around the centre
- **THEN** each child's box is at the same offset on Photon and in the browser, and the node is 240 by 240

#### Scenario: A masonry grid sized by its children

- **WHEN** a delegate overrides `Layout`, lays out children of different heights in two columns of half the room's
  width and answers the height of the taller column, inside a column whose height is sized by content
- **THEN** the node is the room's width and that height, and every child sits under the one before it in its
  column, alike on both targets

### Requirement: The delegate is told the room a filling box has

The delegate SHALL be told the room as a `LayoutBuilder` is: on an axis the parent decides, that size as the
maximum; on an axis sized by content, an unbounded maximum; and zero minimums. The node SHALL be the room's size on
an axis the parent decides and the delegate's size on an axis sized by content, and a delegate that answers an
infinite size on an unbounded axis SHALL be refused with a message naming it.

#### Scenario: Inside a column

- **WHEN** a custom layout stands in a 320-wide column that sizes its height from its children
- **THEN** the delegate is told a maximum width of 320 and an unbounded maximum height, on both targets

### Requirement: Each child is laid out once, by an id it carries

A child SHALL be laid out at most once per layout; a second `LayoutChild` for the same id, an id no child carries,
and two children with one id SHALL be refused. A child the delegate does not lay out SHALL take no room and SHALL
not be drawn. `HasChild(id)` SHALL answer whether a child carries that id. Ids SHALL be compared by `Equals` on
Photon and by value in the browser.

#### Scenario: A missing optional child

- **WHEN** a delegate lays out its badge only when `HasChild("badge")`, and the node has no badge
- **THEN** the layout runs without it on both targets

### Requirement: A single child is laid out with its own delegate

A `CustomSingleChildLayout` SHALL size itself with `GetSize(constraints)`, measure its child with
`GetConstraintsForChild(constraints)` and place it at `GetPositionForChild(size, childSize)`, the room's biggest,
the room and the origin by default.

#### Scenario: A child centred in a fixed box

- **WHEN** a delegate answers a 200 by 100 size and places its child at the centre
- **THEN** a 50 by 20 child is at (75, 40) on both targets

### Requirement: The server writes the children and the browser places them

The server SHALL write the node's children, each in a wrapper at the node's origin, inside a box hidden until the
browser's first layout places them. The browser SHALL run the delegate after its own layout, place each child, and
run it again when the room or a child's size changes, with nothing built.

#### Scenario: Hydration keeps the server's children

- **WHEN** a page with a custom layout is served and the browser hydrates it
- **THEN** the children the server wrote are the ones the browser places, and the box is shown once they are placed

### Requirement: The children are read in their order

The reading order of a custom layout's children SHALL be the order of its children list, on every target, wherever
the delegate places them.

#### Scenario: A layout placing the last child first

- **WHEN** a delegate places the third child at the top left and the first at the bottom right
- **THEN** a screen reader reaches the first child first, on the web and on Photon

### Requirement: Email refuses a custom layout

The email walk SHALL refuse a `CustomMultiChildLayout` and a `CustomSingleChildLayout`, with the reason it refuses
a `LayoutBuilder`.

#### Scenario: A custom layout in a message

- **WHEN** an email's tree holds a `CustomMultiChildLayout`
- **THEN** the email build fails naming the node and the reason
