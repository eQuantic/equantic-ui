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
