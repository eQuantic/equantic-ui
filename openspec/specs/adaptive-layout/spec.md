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
