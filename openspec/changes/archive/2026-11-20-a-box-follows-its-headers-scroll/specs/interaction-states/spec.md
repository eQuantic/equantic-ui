## ADDED Requirements

### Requirement: A box follows the scrolled state of the header it is in

A box's `Scrolled` diff SHALL apply, every member of it, while the nearest `Pinned` around the box is
scrolled, which is while that header's surface has scrolled past `Pinned.ScrolledThreshold`, on the web
and on Photon, under the box's own `Transition`. A box inside no header SHALL never show it, and a header
SHALL be tracked whether or not it declares a `ScrolledStyle` of its own.

#### Scenario: The badge gives way to the mark

- **WHEN** a header holds a box with `Scrolled = new StyleDiff { Opacity = 0, Transform = Transform2D.Scale(0.6f) }`
  and the page scrolls past 8px
- **THEN** the web writes the opacity and the transform under a rule that selects on the header's
  `data-eq-scrolled`, and Photon draws the box faded and scaled while the scroll view is past 8dp

#### Scenario: A box in no header

- **WHEN** a box with a `Scrolled` diff is in no `Pinned`, on a page that has scrolled
- **THEN** the box keeps its resting style on both targets

#### Scenario: A header nested in another

- **WHEN** a box is in a header pinned inside a ScrollView that is itself inside a pinned header, and only
  the outer header is scrolled
- **THEN** the box keeps its resting style; when only the inner header is scrolled, it shows its diff

### Requirement: A control's states are laid over the scrolled diff

When a box is in a scrolled header and its own or its control's state applies too, the state SHALL win
over the `Scrolled` diff, member by member, and the `Scrolled` diff SHALL win over the box's resting
style, on both targets, whatever order the rules reach the stylesheet in.

#### Scenario: A link in a scrolled header is hovered

- **WHEN** a box declares a `Background`, a `Scrolled` diff and a `Hover` diff that each set the fill,
  its header is scrolled and the pointer is over it
- **THEN** the box shows the hover's fill on both targets, and with the pointer away, the scrolled fill
