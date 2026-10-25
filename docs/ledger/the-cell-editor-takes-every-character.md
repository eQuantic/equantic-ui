- **2026-10-25 · A cell's editor takes every character a keyboard types**: a cell's entry became the code
  engine's document, edited through the platform's own text door on both targets, so a dead key, an input
  method's composition, an emoji and AltGr reach the cell, and a caret, a selection, word moves and undo
  live inside it. Enter and Edit modes, the line break, the fill of a selection, the date and time chords,
  Backspace that edits and an editor that grows over its neighbours came with it, and the formula bar and
  the name box joined the family as components the app places beside the grid, views of one controller.
  Escape releases Tab, so a keyboard can leave a sheet ([#732](https://github.com/eQuantic/equantic-ui/issues/732)).
  On the way a single line of text began keeping its spaces on the web, as Photon and Flutter keep them,
  and the code engine learned to hold one line. Proved in Chromium on the dashboard's `/sheet` and in a
  real PhotonDesktop window through the macOS shell's text input client (dead keys and a Japanese
  composition), and
  benched at 100 000 × 100: a character into an open entry 3.0 ms on Photon and 9.5 ms in the runtime
  ([#983](https://github.com/eQuantic/equantic-ui/issues/983)). Proposed and archived through OpenSpec
  (`openspec/specs/spreadsheet`, `openspec/specs/code-editor`).
