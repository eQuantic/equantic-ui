## ADDED Requirements

### Requirement: A container sees through a component to a node it places

A flex line and a grid SHALL place a `Spacer`, a `Flexible` or an `AdaptiveNode` a component builds, through
any components in between, as they place one written directly, on the web and on Photon, and the
component SHALL build once. Any other child SHALL keep the component as the item.

#### Scenario: A Spacer a component builds

- **WHEN** a column holds a box 10 tall, a component that builds `Spacer.Fixed(24)`, and a box
- **THEN** the second box stands at 34 on Photon, as with the Spacer written directly

#### Scenario: A Flexible a component builds

- **WHEN** a row 400 wide holds a component that builds `Flexible(pane, flex: 3)` and `Flexible(pane, flex: 1)`
- **THEN** they are 300 and 100 wide on Photon

#### Scenario: An arm's alignment through a component

- **WHEN** a row holds a component that builds an `AdaptiveNode` whose arm says `AlignSelf = End`
- **THEN** the arm aligns to the end of the line, on both targets, through two components too

#### Scenario: An arm's span through a component

- **WHEN** a grid holds a component that builds an `AdaptiveNode` whose arm says `GridSpan = 2`
- **THEN** the arm spans two tracks, on both targets

#### Scenario: An AdaptiveNode further down

- **WHEN** a component builds a box that holds an `AdaptiveNode`
- **THEN** the arm is not placed by the line the component stands in
