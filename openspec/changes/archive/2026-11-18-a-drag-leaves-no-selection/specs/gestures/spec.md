## ADDED Requirements

### Requirement: A drag on the web leaves no text selected

Every pointer gesture on the web's gesture core (a sortable list, a draggable, a sheet dismissed by dragging)
SHALL clear the text selection its press started once the gesture arms, and SHALL keep any selection
from starting or growing until the gesture ends, when the page's own `user-select` SHALL be given back. A
press that never becomes a gesture SHALL leave the selection to the browser.

#### Scenario: A sortable item made of text

- **WHEN** a mouse drags a table's heading, a text with no press of its own, past another heading
- **THEN** the column moves, and no text of the page is selected

#### Scenario: A press that does not travel

- **WHEN** a mouse presses on text in a sortable item and releases it without travelling past the slop
- **THEN** whatever the browser selected stays selected

#### Scenario: Text outside any gesture

- **WHEN** a mouse drags across ordinary text after a gesture has ended
- **THEN** the text is selected as the browser selects it
