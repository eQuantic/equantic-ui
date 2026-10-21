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
