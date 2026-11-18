## ADDED Requirements

### Requirement: The look of a carried item is the theme's and the list's

The look of an item carried from a `Sortable` SHALL be the theme's `SortableTheme` with the list's own `Style` laid
over it, member by member, and the defaults of `SortPlacement` where neither sets a member: the item in its place
SHALL be drawn at `LiftedOpacity` (0.4), the copy that follows a pointer at `CopyOpacity` (0.92) under the theme's
shadow for elevation level `CopyElevation` (4), and the copy SHALL lean with the pointer's speed no further than
`MaxLean` degrees (12) either way, staying upright at 0. The look SHALL be the one of the list the item was picked up
from, on the web and on Photon.

#### Scenario: A theme's look on the web

- **WHEN** the theme's `SortableTheme` sets `LiftedOpacity` to 0.25 and `CopyElevation` to 5, and a mouse carries card
  `a` of a list with no style of its own
- **THEN** card `a` is drawn at opacity 0.25, and the copy carries the theme's level 5 shadow at opacity 0.92

#### Scenario: A list's own look over the theme's

- **WHEN** the theme sets `MaxLean` to 6 and the list's `Style` sets `MaxLean` to 0, and a mouse carries a card fast
  across
- **THEN** the copy stays upright

#### Scenario: The defaults

- **WHEN** neither the theme nor the list sets a `SortableTheme`
- **THEN** the carried item is drawn at 0.4, and the copy at 0.92 under the theme's level 4 shadow, leaning up to 12°

#### Scenario: The same look on Photon

- **WHEN** the same theme and list are drawn on Photon and a mouse carries card `a`
- **THEN** the host draws card `a` at the theme's `LiftedOpacity` and the copy at the list's `CopyOpacity`, under the
  theme's shadow for `CopyElevation`, leaning no further than `MaxLean`
