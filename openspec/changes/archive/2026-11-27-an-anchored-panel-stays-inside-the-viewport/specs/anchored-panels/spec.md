## ADDED Requirements

### Requirement: An anchored panel stays inside the viewport's width

An `Anchored` panel its placement would put past the viewport's start or end edge SHALL be moved back inside,
`Anchored.ViewportInset` from that edge, and one wider than the room between the two insets SHALL start at the start
edge's, on the web and on Photon alike. A `DatePicker` SHALL hang its calendar `BottomEnd`, under the end of its field.

#### Scenario: A date picker on a field that spans the viewport

- **WHEN** a date picker's field spans a 390, a 768 or a 1280 wide viewport, and its calendar opens
- **THEN** the whole calendar lies inside the viewport, its inset from each edge, and its end sits under the opener's

#### Scenario: A panel past the start edge

- **WHEN** a panel hung `BottomEnd` from a 40dp anchor at the start of a 360 viewport would start before it
- **THEN** it starts at the inset, and so does one wider than the viewport

#### Scenario: A panel on the web

- **WHEN** the web lays a panel out from 306 to 640 in a 390 viewport
- **THEN** the runtime moves it by its `translate` to end at 382, and measures it again from where its placement put it
