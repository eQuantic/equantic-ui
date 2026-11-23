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
SHALL take the focus. On the web, a layer inside an adaptive arm the window hides SHALL NOT hold the keyboard: the
topmost layer whose every arm is on screen SHALL own it, asked when the Tab or the focus arrives.

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

#### Scenario: A drawer in an arm the window hides

- **WHEN** a drawer is open in the compact arm at 700, the window widens to 1024, and back to 700
- **THEN** at 700 the drawer cycles Tab, at 1024 Tab walks the page and a focus outside the drawer stays there,
  and back at 700 the drawer holds Tab again

#### Scenario: A layer outside every arm

- **WHEN** a dialog that stands in no adaptive arm is open at 500, 700 and 1024
- **THEN** at each width a focus behind it is pulled back into it

#### Scenario: A hidden layer over a shown one

- **WHEN** a dialog on the page is open under a drawer in the compact arm, and the window is at 1024
- **THEN** the dialog cycles Tab

### Requirement: A modal layer gives the focus back when it closes

When a modal layer opens, it SHALL record the control that had the focus, on every target. Photon's
layer SHALL then take the focus itself: on the control marked `InitialFocus`, else on its first stop,
unless the focus is inside it already. The web SHALL record the opener and take the focus in the pass
that writes the layer, against the DOM that pass wrote.

When the layer closes, whatever closed it, the focus SHALL go back to that control. When that control
is gone, the focus SHALL go to the control that now stands at its place among those Tab reaches beneath
the layer. Beneath a modal layer that stays open, that place SHALL be inside that layer.

A menu's item SHALL leave the keyboard where it is when it is pressed, so a layer an item opens records
the control that had the focus before: the menu's trigger, which keeps it while the menu shows.

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

#### Scenario: A dialog opened from a menu item

- **WHEN** a row's menu is opened, its Delete item chosen by a pointer or by the keyboard, and the confirm
  it opened closed with Cancel
- **THEN** the focus is on the menu's trigger, on the web and on Photon

#### Scenario: A dialog a page's render opens

- **WHEN** a page's render opens a confirm in the browser, and no later pass comes
- **THEN** the confirm is trapped and holds the focus, and its Cancel gives the focus back to the opener
