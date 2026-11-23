## ADDED Requirements

### Requirement: A modal layer holds the Tab walk on every target

While a modal and open layer is shown, Tab and Shift+Tab SHALL walk the controls of the topmost such layer only,
on the web and on Photon, and SHALL wrap inside it. A layer opened from inside it SHALL be part of its walk, and
a non-modal layer SHALL NOT be. A modal layer with no control SHALL hold the keyboard, and no control behind it
SHALL take the focus.

#### Scenario: Tab from a dialog's last control

- **WHEN** a dialog over a page with a button has two controls, and Tab is pressed from its last one
- **THEN** the focus moves to the dialog's first control, and the page's button is never reached

#### Scenario: Shift+Tab from a dialog's first control

- **WHEN** Shift+Tab is pressed from a dialog's first control
- **THEN** the focus moves to the dialog's last control

#### Scenario: A dialog over a dialog

- **WHEN** two dialogs are shown, one over the other, and Tab is pressed
- **THEN** the focus walks the controls of the dialog on top only

#### Scenario: A menu open in a dialog

- **WHEN** a dialog holds a menu that is open, and Tab is pressed through it
- **THEN** the menu's items are part of the walk, and the walk wraps to the dialog's first control

#### Scenario: A toast

- **WHEN** a non-modal layer is shown over a page and no modal layer is, and Tab is pressed
- **THEN** the walk visits the page's controls and the layer's alike

#### Scenario: A dialog with no control

- **WHEN** a dialog with no control is shown over a page with a button, and Tab is pressed
- **THEN** no control takes the focus, and the page's button is not reached
