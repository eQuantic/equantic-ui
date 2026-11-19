## MODIFIED Requirements

### Requirement: The clipboard keys follow the host's clipboard

A host that hands the spreadsheet a clipboard SHALL have the copy, cut and paste chords (⌘C, ⌘X and
⌘V on Apple's keyboards, Ctrl elsewhere) claimed and served through it. At rest they SHALL carry the
selected cells as tab-separated values; while a cell is being edited they SHALL carry the entry's
selected text, and a paste SHALL insert the clipboard's text at the caret as one step of the entry's
undo. A host that hands none SHALL find those chords unclaimed, so the platform's own copy, cut and
paste events carry the same text. A cut SHALL clear the selection only when its values reached a
clipboard.

#### Scenario: A copy on Photon

- **WHEN** B2:C3 holds `1`, `2`, `3` and `4` row by row, and ⌘C is pressed over it on Photon with a
  clipboard
- **THEN** the clipboard holds `1\t2\n3\t4`

#### Scenario: A copy in the browser

- **WHEN** ⌘C is pressed over a focused sheet in the browser
- **THEN** the key is not claimed, and the copy event that follows carries the selection's
  tab-separated values

#### Scenario: A cut with nowhere to go

- **WHEN** ⌘X is pressed over B2 holding `7` on a host that handed no clipboard
- **THEN** B2 still holds `7`

#### Scenario: A copy while editing

- **WHEN** B2 is being edited with the entry `hello world` and `world` selected, and ⌘C is pressed,
  on Photon with a clipboard and in the browser through its copy event
- **THEN** the clipboard holds `world`, and B2 is still being edited

#### Scenario: A paste while editing

- **WHEN** B2 is being edited with the entry `ab` and the caret between `a` and `b`, and the
  clipboard's text `x\ty` is pasted
- **THEN** the entry is `ax\tyb`, no other cell changed, and one undo while editing gives back `ab`

#### Scenario: A clipboard that answers later

- **WHEN** ⌘V is pressed over B2 with a clipboard whose read has not answered yet, and it then answers `x\ty`
- **THEN** the key is claimed, B2 is empty until the clipboard answers, B2 and C2 hold `x` and `y` after, and
  the sheet's views are told to build again once, after the paste
