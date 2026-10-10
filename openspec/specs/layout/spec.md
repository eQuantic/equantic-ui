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
