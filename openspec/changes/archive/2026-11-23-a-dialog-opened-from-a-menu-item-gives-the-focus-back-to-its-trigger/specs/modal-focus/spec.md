## MODIFIED Requirements

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
