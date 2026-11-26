## ADDED Requirements

### Requirement: A drag's click guard leaves with its click or its owner

On the web, the click a browser sends where an armed drag on the gesture core was released SHALL reach nothing under the pointer. The guard that stops it SHALL be owned by the controller that followed the drag. The guard SHALL go, timer and listener, when it stops that click, and at once when its controller lets go. Otherwise it SHALL go 50 ms after the release. No timer of it SHALL be left once its controller let go.

#### Scenario: The release's click

- **WHEN** a pointer drags a draggable past the slop and lifts, and the browser sends its click
- **THEN** the click presses nothing, and the guard goes with its timer

#### Scenario: The controller lets go right after a drag

- **WHEN** a pointer drags a draggable past the slop and lifts, and its controller is reset before any click
- **THEN** no timer of the guard is left, and a click afterwards is the page's
