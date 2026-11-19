## ADDED Requirements

### Requirement: A resize keeps the keyboard on the sheet

A press on a resize grip drawn inside a spreadsheet's surface SHALL leave the keyboard on the sheet, on the web
as on Photon, so the keys that follow, ⌘Z among them, reach the sheet.

#### Scenario: A grip pressed on the web

- **WHEN** a sheet with B2 selected has a resize grip inside its surface pressed with a mouse
- **THEN** the sheet's text control holds the focus, the `mousedown` is prevented, and B2 is still the
  selection
