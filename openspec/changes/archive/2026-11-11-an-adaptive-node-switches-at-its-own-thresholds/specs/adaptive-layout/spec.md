## ADDED Requirements

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
