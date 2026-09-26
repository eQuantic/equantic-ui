# Spec Delta

## Purpose

How the design system in `docs/design` is held to the SDK: the figures its pages print, the status
of each block, the tokens it publishes, and the contrast its palette must clear.

## ADDED Requirements

### Requirement: A figure a design page prints is its token's value

A number a design page prints that derives from a token SHALL carry the token's path in a
`data-token` marker and SHALL equal the value the SDK's pin holds for that path. A number the SDK
counts SHALL carry a `data-figure` marker and SHALL be exactly the count. The number of marked
figures SHALL NOT fall below the recorded floor.

#### Scenario: A marked figure drifts from its token

- **WHEN** a page prints `<span data-token="touch.minTarget">44</span>` while `Touch.MinTarget` is 48
- **THEN** `HandoffFigureTests` fails, naming the page, the path and both values

#### Scenario: A re-export drops the markers

- **WHEN** a re-export of the pages leaves fewer marked figures than the floor
- **THEN** `HandoffFigureTests` fails instead of passing with nothing to compare

#### Scenario: A counted figure is not the count

- **WHEN** a page shows `56.9` in a `data-figure="components.count"` marker and the SDK has 56 components
- **THEN** `HandoffFigureTests` fails, where a comparison that truncated first let it pass

### Requirement: A block's status is derived from the SDK

Every item of `docs/design/status.json` that is, or was, a request SHALL carry a probe (a line of a
`PublicAPI` file, or a reference in the source) and SHALL have the status the probe proves, or, when
the SDK cannot prove it, SHALL say why in `checkedByHand`. A shipped item's claims SHALL appear on no
page.

#### Scenario: A request ships

- **WHEN** the probe of an item marked `request` appears in the public API
- **THEN** `HandoffStatusTests` fails until the entry and every page call it shipped

#### Scenario: A shipped item's probe disappears

- **WHEN** the probe of an item marked shipped is no longer found
- **THEN** `HandoffStatusTests` fails

#### Scenario: An item with no probe gives no reason

- **WHEN** an item's probe is null and it has no `checkedByHand` reason
- **THEN** `HandoffStatusTests` fails, naming the item

### Requirement: Every public token is published or exempt

Every public member of the SDK's token classes SHALL be published at a `tokens.json` path that a test
compares, or SHALL be named as exempt with the reason it is not a design value.

#### Scenario: A token class gains a member

- **WHEN** a public member is added to `Sizing` or `Touch` and neither published nor exempted
- **THEN** `HandoffSdkCoverageTests` fails, naming the member

### Requirement: The palette clears APCA as well as WCAG 2

Every text colour the design system defines SHALL clear its APCA floor, in the light and the dark
theme, against the lowest-contrast of `Background`, `Surface` and `SurfaceSubtle`: Lc 75 for Primary
and Secondary text, and Lc 60 for Muted and Link. Every label on a fill or a Subtle SHALL clear Lc 60
against that fill, and `BorderStrong` SHALL clear Lc 15 against `Surface` and `Background`, the
grounds Foundations §01 measures a field's boundary on. These sit beside the WCAG 2 ratios the design
tokens already hold.

#### Scenario: Muted text in dark mode

- **WHEN** muted text is measured on `SurfaceSubtle` in the dark theme
- **THEN** its contrast is at least Lc 60, where the palette before this change measured Lc 42.2

#### Scenario: The formula is checked before it judges

- **WHEN** `DesignTokenApcaTests` runs
- **THEN** its APCA 0.0.98G implementation first reproduces the reference values, so a wrong constant fails before any pair is judged
