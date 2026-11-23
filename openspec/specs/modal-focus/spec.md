# modal-focus Specification

## Purpose
How a modal layer's focus trap shares Tab with the control that has the focus: the control answers
first, and the trap cycles only a Tab nothing consumed.

## Requirements

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

### Requirement: A modal layer gives the focus back when it closes

When a modal layer opens, it SHALL record the control that had the focus, on every target. Photon's
layer SHALL then take the focus itself: on the control marked `InitialFocus`, else on its first stop,
unless the focus is inside it already.

When the layer closes, whatever closed it, the focus SHALL go back to that control. When that control
is gone, the focus SHALL go to the control that now stands at its place among those Tab reaches beneath
the layer. Beneath a modal layer that stays open, that place SHALL be inside that layer.

#### Scenario: Cancel

- **WHEN** a row's Delete opens a confirm dialog, and its Cancel closes it
- **THEN** the focus is on that row's Delete, on the web and on Photon

#### Scenario: The row the dialog deleted

- **WHEN** a row's Delete opens a confirm dialog, and its Delete removes that row and closes it
- **THEN** the focus is on the next row's Delete, on the web and on Photon

#### Scenario: The last row deleted

- **WHEN** the last row's Delete opens the dialog that removes it
- **THEN** the focus is on the Delete of the row before it

#### Scenario: A dialog over a dialog

- **WHEN** a control of a dialog opens a second dialog, which removes that control and closes
- **THEN** the focus is on the control now at its place in the first dialog, never on the page behind

#### Scenario: A row removed after the dialog closed

- **WHEN** the confirm closes first, and the row goes in a later pass, when the server answers, with the
  focus still on the row's Delete
- **THEN** the focus is on the next row's Delete; had the user moved the focus meanwhile, it stays where
  they moved it

#### Scenario: Two dialogs closing together

- **WHEN** a confirm opened from a settings dialog closes both at once
- **THEN** the focus is on the settings dialog's opener, on the web and on Photon
