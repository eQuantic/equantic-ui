## ADDED Requirements

### Requirement: A normalized drag on the web measures against the box its rest is written in

On the web, a normalized `Draggable` SHALL divide the finger's travel by the surface's extent without
the surface's own scale, and SHALL write its value back in the box a `translate` percentage resolves
against, so the surface stays under the finger from its rest. What scales the page around the surface
SHALL stay in the division.

#### Scenario: A surface with a scale of its own

- **WHEN** a normalized `Draggable` that follows, 200 wide with a `scale(.5)` and a rest of 0.25, is dragged 14px and then 40px
- **THEN** in Chrome it moves 14px and then 40px from where it rested, with no jump, and a release reports 0.45
