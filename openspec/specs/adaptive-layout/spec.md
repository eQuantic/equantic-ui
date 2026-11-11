# adaptive-layout Specification

## Purpose
How an AdaptiveNode's arms reach the screen: the gates the web shows each arm behind, named from the
node's thresholds, and the parent that lays each arm out on every target.

## Requirements

### Requirement: A gate's name is a class a selector can reach

The web SHALL show each arm of an AdaptiveNode behind a gate whose class names its range in dp,
spelling a fractional threshold's point as an underscore, and SHALL write the media condition of that
range with a dot. Every rule of a gate SHALL apply, so one arm shows at a time whatever the
thresholds.

#### Scenario: A fractional threshold

- **WHEN** a Column holding `AdaptiveNode(Text("narrow"), null, Text("wide")) { ExpandedFrom = 703.7037f }`
  is rendered on the server
- **THEN** its gates are `eq-vc703_7037` and `eq-vx703_7037`, their rules switch at
  `(min-width: 703.7037px)`, and in a browser "narrow" alone shows at 703px and "wide" alone at 704px

#### Scenario: A middle range closes short of a fractional threshold

- **WHEN** an AdaptiveNode with three arms has `MediumFrom = 600` and `ExpandedFrom = 703.7037f`
- **THEN** its gates are `eq-vc600`, `eq-vm600-703_7037` and `eq-vx703_7037`, and the middle arm shows
  from `(min-width: 600px)` to `(max-width: 703.6837px)`

#### Scenario: A whole threshold keeps its name

- **WHEN** an AdaptiveNode with three arms keeps the spec's thresholds
- **THEN** its gates are `eq-vc600`, `eq-vm600-840` and `eq-vx840`, the middle one shown up to
  `(max-width: 839.98px)`

### Requirement: Both web producers spell a threshold alike

The server and the browser SHALL derive the same gate name and the same rules from the same threshold,
written to the ten-thousandth of a dp as rounded from the threshold's exact single value.

#### Scenario: A threshold past a thousand dp

- **WHEN** an AdaptiveNode has `ExpandedFrom = 1066.6667f`
- **THEN** the server and the browser both name its expanded gate `eq-vx1066_6667`, shown from
  `(min-width: 1066.6667px)`

### Requirement: An arm is laid out by the parent the node stands in

An AdaptiveNode SHALL NOT be laid out itself. On the web every arm, and on Photon the arm the window
resolves, SHALL be laid out by the node's parent as that parent lays out a direct child: on the
parent's axis, so a Spacer arm is space along it; anchored at its offsets in a Stack when it is a
Positioned; and placed in its line or grid by its own `AlignSelf` and `GridSpan`. The node's own
`AlignSelf` and `GridSpan` SHALL NOT be read.

#### Scenario: A Gap arm in a Column

- **WHEN** a Column holds `Text("above")`, `AdaptiveNode(Gap(24), null, Gap(64))` and `Text("below")`
- **THEN** "above" and "below" are 24 apart where the compact arm serves and 64 apart where the
  expanded one does, on the web and on Photon

#### Scenario: A Gap arm in a Row

- **WHEN** a Row holds `AdaptiveNode(Gap(24), null, Gap(64))`
- **THEN** on the web the compact arm is a 24px width that does not shrink

#### Scenario: A Positioned arm in a Stack

- **WHEN** a Stack holds a 400 × 300 Box and `AdaptiveNode(Box(), null, Positioned(child, top: 0, end: 0))`
- **THEN** where the expanded arm serves, the child is anchored at the stack's top end corner, and on
  Photon the stack stays 400 wide

#### Scenario: An arm's own alignment and span

- **WHEN** an arm says `AlignSelf = CrossAlign.End` in a Row whose `Cross` is `Start`, and an arm says
  `GridSpan = 2` in a Grid of two columns
- **THEN** the first aligns to the end of its line and the second spans both columns, on the web and
  on Photon

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

### Requirement: Every target switches an arm at the node's own thresholds

An adaptive node's arm SHALL be resolved by the window's width, at the node's own `MediumFrom` and
`ExpandedFrom`, on every target: the web's gates SHALL show it, Photon's layout SHALL lay it out and paint
it, and the element tree SHALL hold it. No target SHALL resolve an arm by the spec's size-class thresholds
for a node that declares its own.

#### Scenario: The dashboard's payments page at 900 dp

- **WHEN** a node with a compact and an expanded arm has `ExpandedFrom = 1024`, at a window 900 dp wide
- **THEN** the element tree holds the compact arm, Photon paints it, and the web's gate shows it while the
  expanded arm's hides it

#### Scenario: A threshold below the spec's

- **WHEN** the same node has `ExpandedFrom = 703.704`, at 704 dp
- **THEN** every target shows the expanded arm
