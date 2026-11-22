## ADDED Requirements

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
