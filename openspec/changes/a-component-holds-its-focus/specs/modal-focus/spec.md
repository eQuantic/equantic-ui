## MODIFIED Requirements

### Requirement: A modal layer holds the Tab walk on every target

While a modal and open layer is shown, Tab and Shift+Tab SHALL walk the controls of the topmost such layer only,
on the web and on Photon, and SHALL wrap inside it. The layer SHALL be a closed-loop focus scope of the one
traversal (`focus-traversal`), so `NextFocus()` and `PreviousFocus()` called from code walk it exactly as Tab
does, and a `RequestFocus()` on a node behind it moves nothing. A layer opened from inside it SHALL be part of its walk, and
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

#### Scenario: NextFocus from code inside a dialog

- **WHEN** a dialog over a page with a button has two controls, the last one has the keyboard, and a handler
  calls `NextFocus()` on its node
- **THEN** the dialog's first control has the keyboard, and the page's button is never reached

#### Scenario: A request behind a dialog

- **WHEN** a dialog is open and a handler calls `RequestFocus()` on the node of a button on the page behind it
- **THEN** the control in the dialog keeps the keyboard
