## ADDED Requirements

### Requirement: A Flexible's slot sizes the item, and a fixed child keeps its own size

A `Flexible` in a single line SHALL take its slot on Photon (its share, its basis, or what an
overflowing line left it), and its child SHALL keep a main size it declares (a fixed width, an
`Image`, an `Icon`, seen through transparent wrappers), wider than the slot or narrower, as a browser
keeps a fixed child inside its flex item and lets it overflow. A child that declares no main size
SHALL fill the slot: an auto or Fill box, and a `Text`, whose line box the slot is.

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
