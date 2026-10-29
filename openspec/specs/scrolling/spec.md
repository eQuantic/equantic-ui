# scrolling Specification

## Purpose
How the wheel and the trackpad reach a scroller on Photon: on the axis they move, with the platform's
conventions for a wheel that only moves one way.

## Requirements

### Requirement: A wheel and a trackpad move a scroller on the axis they move

On Photon a trackpad's horizontal movement and a mouse's horizontal wheel SHALL reach the scroller
under the pointer on the horizontal axis, on macOS and on Windows. Shift with a vertical wheel SHALL
scroll horizontally on Windows, as the platform leaves that to the app; macOS already turns it into a
horizontal movement. A vertical wheel over a scroller that only scrolls horizontally SHALL move it
sideways, as it does today.

#### Scenario: A horizontal swipe on a Mac trackpad

- **WHEN** a trackpad swipes left over a horizontal scroll view on macOS
- **THEN** the scroll view moves right by the swipe's distance

#### Scenario: The tilt wheel on Windows

- **WHEN** a mouse wheel tilts right over a horizontal scroll view on Windows
- **THEN** the scroll view moves right

#### Scenario: Shift and the wheel on Windows

- **WHEN** the wheel moves down with Shift held over a horizontal scroll view on Windows
- **THEN** the scroll view moves right

### Requirement: A scroll view goes where the app asks, once per request

A `ScrollView` whose `Request` carries a version it has not honoured SHALL scroll to the request's offset,
clamped to what its content can scroll, on the web and on Photon, and SHALL report the offset it went to
through `OnScrolled`. A request it has honoured SHALL move nothing, however often the tree is built again,
so a reader's own scrolling after it stays. A glide whose range shrinks under it SHALL land at the new end.

#### Scenario: A request after the first frame

- **WHEN** a scroll view over content 2000 high in a viewport 300 high is drawn at offset 0, and built again
  with a request of version 1 for offset 900
- **THEN** it shows the content from 900, and `OnScrolled` reported 900

#### Scenario: A request already honoured

- **WHEN** that view is scrolled by the reader to 400 and built again with the same request
- **THEN** it stays at 400

#### Scenario: Past the end

- **WHEN** it is built with a request of version 2 for offset 5000
- **THEN** it shows the content from 1700, the end, and stays there when its content grows

### Requirement: A scroll view's content can fill its viewport

A `ScrollView` whose `FillViewport` is set SHALL lay its content out never smaller than its viewport, on the
web and on Photon, in the layout that sizes the viewport and with no size reported back: the content SHALL
take the viewport's extent on each axis on which it is smaller, and SHALL scroll only where it is longer. A size
the content states on an axis, a fixed one or the window's, SHALL stand. The viewport SHALL be the
room inside the scroller, with no scrollbar that takes space in it, so content that fills it never makes an
enclosing scroller scroll. A scroll view whose parent stretches it on an axis it hugs SHALL take the stretched
size on both targets.

#### Scenario: A short content

- **WHEN** a vertical scroll view 100 tall that fills its viewport holds a column 60 tall
- **THEN** the column is 100 tall and the scroll view has nothing to scroll

#### Scenario: A long content

- **WHEN** the same scroll view holds a column 240 tall
- **THEN** the column is 240 tall and the scroll view scrolls by 140

#### Scenario: Sideways, inside a stretched row

- **WHEN** a row 90 tall that stretches its children holds a horizontal scroll view that fills its viewport,
  around a box of 50 by 20
- **THEN** the scroll view is 90 tall and its content 90 tall
