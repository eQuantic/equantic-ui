## MODIFIED Requirements

### Requirement: A normalized drag on the web measures against the box its rest is written in

On the web, a normalized `Draggable` SHALL divide the finger's travel, read in the space its offset
moves in, by the surface's untransformed box, the one a `translate` percentage resolves against, and
SHALL write its value back in that same box, so the surface stays under the finger from its rest. No
transform, the surface's own or an ancestor's, SHALL enter the division.

#### Scenario: A surface with a scale of its own

- **WHEN** a normalized `Draggable` that follows, 200 wide with a `scale(.5)` and a rest of 0.25, is dragged 14px and then 40px
- **THEN** in Chrome it moves 14px and then 40px from where it rested, with no jump, and a release reports 0.45

#### Scenario: A surface turned by its own transform

- **WHEN** a normalized horizontal `Draggable` 200 wide and 40 thick, turned 90° by its own transform, is dragged 40px sideways
- **THEN** in Chromium its offset is 40px, drawn 40px away, and a release reports 0.2, where it jumped to 1

#### Scenario: A surface under a turned ancestor

- **WHEN** the same `Draggable`, unturned, sits in a box turned 90° and is dragged 40px down the screen
- **THEN** in Chromium its offset is 40px, drawn 40px down the screen, and a release reports 0.2
