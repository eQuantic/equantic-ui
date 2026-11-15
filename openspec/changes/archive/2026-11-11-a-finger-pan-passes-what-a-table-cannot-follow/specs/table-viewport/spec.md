## MODIFIED Requirements

### Requirement: The viewport scrolls the whole grid on every target

The viewport SHALL keep its own offset on both axes, clamped between zero and the grid's extent less
the window's, and SHALL reach the last row and the last column of a grid larger than any element a
browser lays out. A vertical wheel SHALL scroll vertically, Shift with a vertical wheel SHALL scroll
horizontally, and a trackpad SHALL scroll both axes by its movement. A finger dragging the cells SHALL
scroll them with it on both axes, and a finger lifted while moving SHALL glide to rest within the
extent. A mouse's or a trackpad pointer's drag over the cells SHALL be the cells', on a phone or a
tablet as on a desktop, where the platform says which tool it is. The movement the viewport cannot take on an axis, at its end, SHALL pass to the scroller around
it on that axis, from a wheel and from a finger alike: a finger's pan moves that scroller by its travel
less what the grid took since the viewport took the pan. A drag surface inside a cell SHALL keep its
own gesture under a finger.

#### Scenario: The last row of Excel's grid in a browser

- **WHEN** a table of 1 048 576 rows of 28 is scrolled to its end in Chromium
- **THEN** row 1 048 576 is drawn with its bottom edge at the bottom of the window

#### Scenario: Shift and the wheel

- **WHEN** the wheel moves three lines down with Shift held over the cells
- **THEN** the viewport moves to the right by three lines and not down

#### Scenario: A fling

- **WHEN** a finger dragging the cells upward lifts while moving
- **THEN** the cells keep moving, slow down, and come to rest inside the grid's extent

#### Scenario: At the end of the grid

- **WHEN** the viewport is at its last row and the wheel keeps moving down over it inside a scrolling
  page
- **THEN** the viewport stays at its last row and the page scrolls

#### Scenario: A finger at the end of the grid

- **WHEN** the viewport is at its last row inside a scrolling page and a finger pans the cells up by 100
- **THEN** the viewport stays at its last row and the page scrolls by 100

#### Scenario: A finger the grid follows part of the way

- **WHEN** the viewport is two rows of 28 short of its last row inside a scrolling page and a finger pans
  the cells up by 100
- **THEN** the viewport moves to its last row and the page scrolls by the 44 that are left

#### Scenario: A drag surface in a cell

- **WHEN** a finger presses a drag surface inside a cell and moves
- **THEN** the surface takes the drag, and neither the viewport nor the page scrolls
