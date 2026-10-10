# Spec Delta

## ADDED Requirements

### Requirement: A pinned header's scrolled style applies every member on every target

While the surface a `Pinned` header pins to has scrolled past `Pinned.ScrolledThreshold` (8dp), every
member of its `ScrolledStyle` SHALL apply over `Pinned.ScrolledBase`, whose border is the hairline
along the header's bottom edge, on the web and on Photon, under the header's own `Transition`. The
surface SHALL be the nearest ScrollView around the header, or the page when there is none.

#### Scenario: The member a developer reaches for first

- **WHEN** a header's `ScrolledStyle` sets `Elevation = 2`, and the page scrolls past 8px
- **THEN** the web writes the elevation's shadow under the header's `[data-eq-scrolled]` rule, and
  Photon draws it while the scroll view is past 8dp

#### Scenario: A header in a scrolling panel

- **WHEN** a header pinned inside a ScrollView panel is on a page that scrolls, and only the page has
  scrolled
- **THEN** the header is not scrolled; when the panel scrolls past the threshold, it is

#### Scenario: The threshold

- **WHEN** the surface is at 8 and then at 9
- **THEN** the header is not scrolled at 8 and is at 9, on both targets

### Requirement: A state's border follows the edges its box draws

A state's `BorderWidth` SHALL draw along the edges the box's `BorderSides` draws, and alone SHALL draw
in the base's colour, on the web as on Photon.

#### Scenario: A hover border on a box that draws one edge

- **WHEN** a box with `BorderSides = Bottom` declares `Hover = new StyleDiff { BorderWidth = 2 }`
- **THEN** the web's hover rule writes `border-width: 0 0 2px 0` in the base's colour, never a
  shorthand that draws four edges
