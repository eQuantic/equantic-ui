- **2026-10-31 · A resize keeps the keyboard on the sheet on the web**: a Bug ([#1092](https://github.com/eQuantic/equantic-ui/issues/1092)).
  A spreadsheet's header grip is a `Draggable` the sheet steps aside for, and the browser moved the focus to the
  page's body on the press's `mousedown`, so ⌘Z no longer reached the sheet and the resize could not be taken back.
  The sheet's text door takes the keyboard on the grip's `pointerdown` and the `mousedown` is prevented, as on
  Photon, where a press inside a sheet makes it the editing surface first. Proposed and archived through OpenSpec
  (`spreadsheet`).
