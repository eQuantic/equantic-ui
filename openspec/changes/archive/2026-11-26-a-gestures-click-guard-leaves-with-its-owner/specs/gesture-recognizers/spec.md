## ADDED Requirements

### Requirement: A lost press's click guard leaves with its click or the arena

On the web, the `click` the browser fires after the up of a press that lost its arena SHALL reach nothing inside that press. The guard that stops it SHALL be owned by the arena's controller. The guard SHALL go, timer and listener, when it stops that click, and at once when the controller is reset, an ended arena's guard included. Otherwise it SHALL go 50 ms after the arena ends. No timer of it SHALL be left once the controller was reset.

#### Scenario: The click of a press that lost

- **WHEN** a recognizer takes a pointer from a button under it, the pointer lifts, and the browser clicks the button
- **THEN** the button is not pressed, and the guard goes with its timer

#### Scenario: The arena is reset after the press ended

- **WHEN** a recognizer takes a pointer from a button under it, the pointer lifts, and the arena's controller is reset before any click
- **THEN** no timer of the guard is left, and a click on the button afterwards presses it
