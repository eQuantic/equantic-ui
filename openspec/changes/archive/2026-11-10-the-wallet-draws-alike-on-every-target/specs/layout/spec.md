## ADDED Requirements

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
