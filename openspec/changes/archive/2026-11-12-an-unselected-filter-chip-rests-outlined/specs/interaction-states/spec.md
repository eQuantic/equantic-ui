# Spec Delta

## ADDED Requirements

### Requirement: A chip rests, hovers and presses as B8 draws it

An unselected filter `Chip` SHALL rest outlined, with no fill, a 1dp `BorderStrong` edge and
`TextPrimary` text, and SHALL hover and press on `SurfaceSubtle`, on the web and on Photon. An Input chip SHALL draw its
label in `TextPrimary` and its close glyph in `TextMuted`.

#### Scenario: An unselected filter chip at rest

- **WHEN** an unselected filter chip is drawn
- **THEN** both targets draw it with no fill, a 1dp `BorderStrong` edge and a `TextPrimary` label

#### Scenario: An unselected filter chip hovered or pressed

- **WHEN** the pointer rests on an unselected filter chip, or the chip is pressed
- **THEN** both targets fill the chip whole with `SurfaceSubtle`

#### Scenario: An Input chip

- **WHEN** a removable Input chip is drawn
- **THEN** its label is `TextPrimary` and its close glyph `TextMuted`
