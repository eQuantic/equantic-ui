## ADDED Requirements

### Requirement: A hold's click guard leaves with its click or its controller

On the web, the click a browser sends where a finger's hold that asked for a menu was released SHALL reach nothing under the finger. The guard that stops it SHALL be owned by the context controller. The guard SHALL go, timer and listener, when it stops that click, and at once when the controller is reset. Otherwise it SHALL go 600 ms after the hold asked. No timer of it SHALL be left once the controller was reset.

#### Scenario: The release's click

- **WHEN** a finger holds a context target still until it asks for its menu, lifts, and the browser sends its click
- **THEN** the click presses nothing, and the guard goes with its timer

#### Scenario: The controller is reset right after a hold

- **WHEN** a finger holds a context target still until it asks for its menu and lifts, and the controller is reset before any click
- **THEN** no timer of the guard is left, and a click afterwards is the page's
