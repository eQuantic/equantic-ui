## ADDED Requirements

### Requirement: A viewport may decline the focus

A viewport told to decline the focus SHALL be no stop of the Tab order itself and SHALL take no key,
on every target, so a component that composes it keeps the keyboard; the controls in its cells SHALL
stay stops of the Tab order.

#### Scenario: A table that declines the focus

- **WHEN** Tab moves through a page holding a table that declines the focus, with a button in one of
  its cells
- **THEN** the focus lands on the button and never on the table itself, and an arrow pressed on the
  button moves no grid

### Requirement: A held drag scrolls the viewport

While a component that composes a viewport reports a pointer held past the edges of the part where
the cells scroll (the pinned bands count as outside it), the viewport SHALL scroll toward that pointer
on every frame, at a speed that grows with how far past the edge it is, until the pointer comes back,
the component reports none, or the grid's end is reached, on every target. Under reduced motion it
SHALL still scroll, by the same distance per elapsed time.

#### Scenario: A pointer below the cells

- **WHEN** a composing component reports a pointer 60 points below the bottom edge of the cells and
  frames run for half a second
- **THEN** the grid has scrolled down, and it stops at the first frame after the component reports no
  pointer

#### Scenario: A pointer over the pinned row

- **WHEN** a grid scrolled down by 500 points is reported a pointer over its pinned row
- **THEN** the grid scrolls up
