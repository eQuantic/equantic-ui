# table-viewport Specification

## Purpose
A window onto a grid of rows and columns larger than any screen: bands held still on its leading
edges, scrolling on both axes on every target, and a table that builds only the cells in view.

## Requirements

### Requirement: A table builds only the cells in view

A table SHALL build the cells of the rows and columns that intersect its window, those of its pinned
bands, and an overscan on each side of the window, and no other. A scroll that keeps the window inside
the cells already built SHALL build nothing, on every target.

#### Scenario: A grid of Excel's size

- **WHEN** a table of 1 048 576 rows of 28 by 16 384 columns of 96, with one pinned row, one pinned
  column and an overscan of 3, is shown in a window 900 wide and 600 tall
- **THEN** the cells built are exactly those of the rows and columns that intersect the window, the
  pinned row and column, and three more rows and columns past each edge of the window, fewer than
  1 000 cells, on the web as on Photon

#### Scenario: A scroll inside the overscan

- **WHEN** that table scrolls down by 28
- **THEN** the table is not built again

### Requirement: Pinned bands hold still while the cells scroll

The first `PinnedRows` rows SHALL stay at the top of the window while the other rows scroll, and SHALL
scroll sideways with their columns. The first `PinnedColumns` columns SHALL stay at the leading edge
while the other columns scroll, and SHALL scroll vertically with their rows. Their shared corner SHALL
stay on both axes. A cell scrolled under a band SHALL be drawn beneath it and SHALL take no press there.
A pinned cell SHALL stay aligned with the cells of its column and its row at every offset and every
width, on every target.

#### Scenario: Headers aligned at every width

- **WHEN** a table with one pinned row and one pinned column is scrolled to 5 000 across and 120 000
  down, in windows 400, 900 and 1 440 wide
- **THEN** the leading edge of every pinned row cell equals the leading edge of its column's cells, and
  the top edge of every pinned column cell equals the top edge of its row's cells, on the web as on
  Photon

#### Scenario: A press on a band

- **WHEN** a press lands on a pinned row cell drawn over a scrolled cell
- **THEN** the pinned cell takes the press

### Requirement: The viewport scrolls the whole grid on every target

The viewport SHALL keep its own offset on both axes, clamped between zero and the grid's extent less
the window's, and SHALL reach the last row and the last column of a grid larger than any element a
browser lays out. A vertical wheel SHALL scroll vertically, Shift with a vertical wheel SHALL scroll
horizontally, and a trackpad SHALL scroll both axes by its movement. A finger dragging the cells SHALL
scroll them with it on both axes, and a finger lifted while moving SHALL glide to rest within the
extent. A mouse's or a trackpad pointer's drag over the cells SHALL be the cells', on a phone or a
tablet as on a desktop, where the platform says which tool it is. The movement the viewport cannot take on an axis, at its end, SHALL pass to the scroller around
it.

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

### Requirement: A scrollbar on each axis the grid overflows

The viewport SHALL draw a scrollbar on each axis whose extent exceeds its window, outside its pinned
bands, in the theme's colours, with a thumb that shows the window's share of the extent and never
falls below a size a pointer can grab. Dragging a thumb SHALL scroll in proportion, and a press on the
track SHALL move the window by a page toward the press. The scrollbars SHALL have the same geometry on
every target.

#### Scenario: A thumb dragged to the end

- **WHEN** the vertical thumb of a 1 048 576-row table is dragged to the bottom of its track
- **THEN** the last row is at the bottom of the window

#### Scenario: A press on the track

- **WHEN** a press lands on the vertical track below the thumb
- **THEN** the window moves down by its own height less one row

### Requirement: The viewport says where it is

The viewport SHALL report its offset on both axes each time it changes, and the size of its window each
time that changes, once per change, on every target.

#### Scenario: One report per change

- **WHEN** a wheel moves the viewport by 84 down and then nothing moves for a frame
- **THEN** one offset report arrives with the new vertical offset and the unchanged horizontal one, and
  no further report arrives in that frame

### Requirement: A request scrolls to an offset or reveals a range

A request SHALL carry a version, and the viewport SHALL honour each version once. A request to an
offset SHALL move the viewport there, clamped. A request to reveal a range of cells SHALL move the
viewport by the least on each axis that brings the range inside the part of the window its pinned
bands leave uncovered. A range that starts in a pinned band and reaches past it SHALL bring in the part
past the band, and one wholly inside a pinned band SHALL move nothing on that axis. A range larger than
that part SHALL have its leading edges brought in. A request
whose version was already honoured SHALL move nothing, even after the user scrolled away. A version
SHALL be honoured against the layout that stands: until the user moves the grid it SHALL be applied
again against each layout, so a viewport measured twice settles where the last measure puts it.

#### Scenario: A cell below the window

- **WHEN** a window shows rows 0 to 20 and a request with a new version reveals row 40
- **THEN** row 40's bottom edge meets the window's bottom edge, and the horizontal offset is unchanged

#### Scenario: A cell under the pinned row

- **WHEN** a request with a new version reveals a cell scrolled up under the pinned row
- **THEN** the cell's top edge meets the pinned row's bottom edge

#### Scenario: A row that gives the table less than it first measured

- **WHEN** a row holds a fixed sibling and a table filling the rest, and a request reveals a column past
  the window
- **THEN** the column's right edge lies inside the width the row finally gave the table

#### Scenario: A version already honoured

- **WHEN** the user scrolls away from a revealed cell and the table builds again with the same request
- **THEN** the viewport does not move

### Requirement: Marks over a range of cells

A mark SHALL be drawn over the rectangle its range of cells covers, above the cells, and SHALL move
with them. Where its range reaches a pinned band the mark SHALL be drawn there too, clipped to that
band, and where it passes under a band it SHALL be hidden beneath it, on every target. A mark SHALL
take no press and no focus, and SHALL be nothing a screen reader reaches, whatever it holds.

#### Scenario: An outline across the pinned row

- **WHEN** a mark covers rows 0 to 5 of a table whose first row is pinned, and the table is scrolled
  down by three rows
- **THEN** the mark is drawn over the pinned row and over rows 4 and 5, and nothing of it shows over the
  pinned row's cells except its own part

#### Scenario: A control inside a mark

- **WHEN** a mark holds a pressable control
- **THEN** the Tab order holds no stop for it, and the table's accessible tree nothing of it

### Requirement: The focus brings its cell into view

When the focus lands on a control in a cell the window does not show, the viewport SHALL scroll, at
once, until that control lies in the part of the window the pinned bands leave uncovered, on the axes
the cell's band moves along, on every target. A box the browser scrolled by itself SHALL be put back.

#### Scenario: Tab past the rows in view

- **WHEN** Tab moves through the controls of a table's cells past the last row the window shows
- **THEN** each focused control is inside the table's window when it is drawn

### Requirement: The keyboard reaches and scrolls the viewport

The viewport SHALL be a stop of the Tab order itself, before its cells' controls, on every target. While
it has the focus, the arrows SHALL move it a step, PageUp and PageDown a page of the part that scrolls,
and Home and End to the first and the last row, with Control or Command to the first and the last
column too; a key that moves nothing SHALL travel on. Tab SHALL visit its cells' controls row by row,
and in each cell in the cell's own order, whatever order the bands are painted in.

#### Scenario: A table of text alone

- **WHEN** a table whose cells hold only text has the focus and PageDown is pressed
- **THEN** the grid moves down by the part of the window under the pinned rows

#### Scenario: A pinned two by two of buttons

- **WHEN** Tab moves from the viewport through a two by two table of buttons with a pinned row and a
  pinned column
- **THEN** the buttons are visited at 0:0, 0:1, 1:0 and 1:1, in that order

### Requirement: A viewport on an unbounded axis takes the visible extent of its scroller

A viewport asked to fill an axis that has no bound SHALL take the visible extent of the nearest
scroller around it on that axis, or the window's when no scroller is around it, and SHALL scroll its
grid inside that extent. A development log SHALL say which viewport took which extent and why, once
per viewport. A width of its own wider than the room its parent gives SHALL be held to that room, on
every target; a height of its own SHALL stay its own.

#### Scenario: A table in a scrolling page

- **WHEN** a table that fills its height is placed in a page whose scroller shows 844 of its height
- **THEN** the viewport is 844 tall, its rows scroll inside it, and the development log names the
  viewport and the page's scroller

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

### Requirement: A mark may glide to where it moves

A mark that names a key and a glide SHALL, when a later build moves the mark of that key to another
range, be drawn moving from the rectangle it stood over to the new one, over the glide's duration and
along its curve, on every target, laid out at each rectangle on its way rather than scaled to it. A mark
with no glide, a mark of a key the previous build did not hold, and every mark under the platform's
reduced-motion setting SHALL be drawn where it stands at once, and a glide turned on or off SHALL leave
the mark where it stands. A gliding mark SHALL be clipped and hidden by the pinned bands as every mark is.

#### Scenario: A mark that moves a column

- **WHEN** a mark keyed `cursor` with a glide of 200 milliseconds covers cell (2, 2), and the next build
  places it over cell (2, 3)
- **THEN** 100 milliseconds later the mark is drawn halfway between the two cells, and 200 milliseconds
  later over cell (2, 3), on the web as on Photon

#### Scenario: A mark that grows

- **WHEN** a gliding mark keyed `cursor` with a 2-point outline covers one column, and the next build
  places it over three
- **THEN** halfway through the glide it is drawn two columns wide with its outline still 2 points thick,
  and what it holds keeps its own size

#### Scenario: A glide turned on in a scrolled grid

- **WHEN** the grid is scrolled a column across and a mark keyed `cursor` with no glide gains one over
  the same cell
- **THEN** the mark is drawn over that cell, and nothing glides

#### Scenario: Reduced motion

- **WHEN** the platform asks for reduced motion and the same mark moves from (2, 2) to (2, 3)
- **THEN** the mark is drawn over (2, 3) on the first frame

#### Scenario: A new mark

- **WHEN** a build adds a mark keyed `cursor` that the previous build did not hold
- **THEN** it is drawn over its range at once
