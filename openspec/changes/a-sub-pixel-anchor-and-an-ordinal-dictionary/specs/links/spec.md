# Spec Delta

## ADDED Requirements

### Requirement: A fragment lands its target below the pinned chrome

A URL whose fragment names an element SHALL leave that element one pinned header below the top of
the window, on a cold load, on a warm load and after a navigation, the header's height being measured
and never declared. When the browser's own jump leaves the target under the chrome before the
height is known, the runtime SHALL scroll it into place once. It SHALL treat as under the chrome a
target whose top is anywhere from one device pixel ABOVE the top of the window, and never less than
one CSS pixel, down to the chrome's edge, because the jump lands a fractional layout position on a
whole-device-pixel scroll offset. A reader a whole device pixel or more past the target SHALL be left
where they are.

#### Scenario: A warm load leaves the target a fraction of a pixel above the top

- **WHEN** a page under a 65px floating header is loaded with `#liability` from the cache, the target
  sits at a document offset of 2471.796875, and the browser's jump scrolls to 2472 before the
  header's height is published
- **THEN** the target ends 65px below the top of the window, where it was left at -0.203125

#### Scenario: A reader past the target

- **WHEN** the target sits a whole device pixel or more above the top of the window, which is two
  CSS pixels on a page zoomed out to a device-pixel ratio of 0.5
- **THEN** the page does not move
