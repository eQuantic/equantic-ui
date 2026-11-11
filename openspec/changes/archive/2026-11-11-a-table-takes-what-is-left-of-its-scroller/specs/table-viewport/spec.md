## MODIFIED Requirements

### Requirement: A viewport on an unbounded axis takes the visible extent of its scroller

A viewport asked to fill an axis that has no bound SHALL take what the nearest scroller around it on
that axis shows from where the viewport starts in that scroller's content, or what the window shows when
no scroller is around it, and SHALL scroll its grid inside that extent. When what is left from where it
starts is less than half of what the scroller shows, it SHALL take the whole extent the scroller shows
instead. A development log SHALL say which viewport took which extent and why, once per viewport. A width
of its own wider than the room its parent gives SHALL be held to that room, on every target; a height of
its own SHALL stay its own.

#### Scenario: A table in a scrolling page

- **WHEN** a table that fills its height is placed at the top of a page whose scroller shows 844 of its
  height
- **THEN** the viewport is 844 tall, its rows scroll inside it, and the development log names the
  viewport and the page's scroller

#### Scenario: A table below the top of a scrolling page

- **WHEN** a table that fills its height starts 36 below the top of a page whose scroller shows 844 of
  its height
- **THEN** the viewport is 808 tall, so its end and its horizontal scrollbar stand where the page's
  visible part ends, on every frame, and the development log says it took what is left from where it
  starts

#### Scenario: A table far down a scrolling page

- **WHEN** a table that fills its height starts 600 below the top of a page whose scroller shows 844 of
  its height
- **THEN** the viewport is 844 tall, the whole extent, since what is left is under half of it
