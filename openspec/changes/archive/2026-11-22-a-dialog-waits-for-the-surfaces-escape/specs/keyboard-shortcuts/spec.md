## ADDED Requirements

### Requirement: A binding of Escape waits for the surface the keyboard is in

A shortcut that binds Escape SHALL leave the key to the code or sheet surface the keyboard is in while that
surface claims Escape, on the web and on Photon, as Flutter dismisses a route only for an Escape nothing under
the focus took: the surface SHALL take the key, and the shortcut SHALL NOT answer it. With nothing of its own to
close, the surface SHALL leave Escape to the shortcut, as before. A chord other than Escape SHALL answer before
the focused surface, as before, and the platform's back SHALL fire the shortcut whatever the surface holds.

#### Scenario: A code editor with its completion list open in a dialog

- **WHEN** a dialog that closes on Escape holds a code editor, the keyboard is in the editor, its completion list
  is open, and Escape is pressed
- **THEN** the list closes, what was typed stays, the dialog stays open, and the next Escape closes the dialog

#### Scenario: A spreadsheet with a cell being edited in a dialog

- **WHEN** a dialog that closes on Escape holds a spreadsheet whose cell is being edited, and Escape is pressed
- **THEN** the edit is cancelled and writes nothing, the dialog stays open, and the next Escape closes the dialog

#### Scenario: An app's chord inside the editor

- **WHEN** a page binds ⌘K around a code editor that holds the keyboard, and ⌘K is pressed
- **THEN** the page's binding answers

#### Scenario: The platform's back over an edit

- **WHEN** a dialog that closes on Escape holds a spreadsheet whose cell is being edited, and the platform's back
  is pressed on Photon
- **THEN** the dialog's binding answers, and the edit is left as it was

#### Scenario: A surface's answer is what its Escape then does

- **WHEN** a code editor, a sheet, a formula bar or a name box is asked whether Escape is its own, at rest and in
  each state that claims it
- **THEN** its answer is what its `HandleKey` then answers for Escape
