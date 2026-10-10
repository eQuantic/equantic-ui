## ADDED Requirements

### Requirement: A modal layer cycles only a Tab the focused control did not take

A modal layer's focus trap SHALL let the control that has the focus answer Tab first, and SHALL cycle
the focus only for a Tab that control did not consume.

#### Scenario: A code editor last in a dialog

- **WHEN** a code editor is the last control of a dialog and Tab is pressed in it
- **THEN** the line is indented and the editor keeps the keyboard

#### Scenario: A code editor first in a dialog

- **WHEN** a code editor is the first control of a dialog, its line indented, and Shift+Tab is pressed
  in it
- **THEN** the line is outdented and the editor keeps the keyboard

#### Scenario: After Escape

- **WHEN** Escape is pressed in a code editor at either end of a dialog, then Tab or Shift+Tab
- **THEN** the focus moves on as the dialog cycles, and the line is left as it was
