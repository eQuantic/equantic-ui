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
