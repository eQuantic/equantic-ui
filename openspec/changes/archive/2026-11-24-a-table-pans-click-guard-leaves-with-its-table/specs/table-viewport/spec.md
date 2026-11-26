## ADDED Requirements

### Requirement: A pan's click guard leaves with its click or its table

On the web, the click a browser sends where a pan on a table viewport ended SHALL reach nothing under the finger, and
the guard that stops it SHALL be the table's: it SHALL go, timer and listener, when it stops that click, and at once
when the table leaves the page or its position is disposed, and SHALL otherwise go 400 ms after the pan. No timer of
it SHALL fire after its table left.

#### Scenario: The table leaves right after a pan

- **WHEN** a finger pans a table viewport and the table is removed before the lift's click
- **THEN** no timer of the table is left, and a click on the page afterwards is the page's

#### Scenario: The lift's click

- **WHEN** a finger pans a table viewport and lifts, and the browser sends its click
- **THEN** the click reaches nothing under the finger, and the guard goes with its timer
