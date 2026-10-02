# Spec Delta

## ADDED Requirements

### Requirement: A hover applies every member of its diff on every target

While a box is hovered, every member its `Hover` diff sets SHALL apply, on the web and on Photon:
background, border colour and width, elevation, opacity, gradient, backdrop blur, transform and
custom shadows. A member the diff leaves unset SHALL keep the base value.

#### Scenario: A hover that lifts a box

- **WHEN** a box declares `Hover = new StyleDiff { Transform = Transform2D.Translate(0, -2) }` and the
  pointer is over it
- **THEN** the web writes `transform: translate(0px, -2px)` under the box's `:hover` class, and Photon
  draws the box translated two units up

#### Scenario: A hover that raises the elevation, on Photon

- **WHEN** a box at elevation 1 declares `Hover = new StyleDiff { Elevation = 3 }` and is hovered
- **THEN** Photon draws elevation 3's shadow, where it drew elevation 1's

### Requirement: A state's shadow list composes the way the base's does

While a state is active, the box's shadow list SHALL be the state's elevation (or the base's), then
the state's custom shadows (or the base's), then the base's inset highlight, so a state that changes
one keeps the others.

#### Scenario: A hover that deepens the elevation of a glowing card

- **WHEN** a box with a custom glow and an inset highlight declares `Hover = new StyleDiff { Elevation = 3 }`
- **THEN** the web's `:hover` `box-shadow` lists elevation 3's shadow, the glow and the inset highlight,
  where it listed elevation 3's shadow alone
