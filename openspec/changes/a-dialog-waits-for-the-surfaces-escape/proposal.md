## Why

Closes #1181, a bug under #419 (the code editor: an IDE-grade, write-once editor). A `CodeEditor` inside a
dismissible Dialog, BottomSheet or Drawer lost every Escape of its own to the layer: with the completion list
open, Escape closed the whole layer, and what was typed in it went with it, where the same editor on the page
closed only the list. Measured in Chromium on the dashboard sample: the list open and the keyboard in the
editor, one Escape closed the sheet and left the focus on the page's body.

Both targets asked the page's bindings first. The web's shortcut controller hears keydown on the window in the
capture phase, before the editor's input, and the reconciler then drops the input's own keydown; Photon's
`PhotonHost.KeyDown` fires a matching binding before it asks the code target. The editor's keymap claims Escape
while something is over the code, and says why: "so the trap on Tab stays armed and a dialog around the editor
stays open". The spreadsheet has the same door: its Escape cancels a cell being edited, and its formula bar and
name box give up their typing on it.

## What Changes

For a developer using the SDK, nothing to write: inside a dialog, a sheet or a drawer, Escape first closes what
the code editor or the spreadsheet the keyboard is in shows (a completion list, a card, a signature panel, a
snippet being walked, a second caret, the go-to field, a cell's edit), and the layer stays open; the next Escape,
with nothing left to close, closes the layer as before. An app's own chords (a page's ⌘K) still answer before a
focused editor, and the platform's back on Android still closes the layer over an edit.

- **The models say whether Escape is theirs.** `ICodeSurfaceModel` and `ISheetSurfaceModel` gain
  `ClaimsEscape(KeyboardConvention)`: whether `HandleKey` would claim Escape now, answered without doing
  anything. `CodeEditorController`, `SheetController`, `SheetFormulaBar` and `SheetNameBox` implement it; the
  code editor reads it from its keymap's own bindings (an app that gives Escape to the host is answered so).
- **The web asks the focus before a binding fires.** The shortcut controller puts a question to the element the
  key is for, an event of the runtime's own (`eq:takes-key-first`); an element that cancels it keeps the key,
  which then reaches its own keydown. A text door keeps Escape while its model claims it.
- **Photon asks its code and sheet targets.** `PhotonHost.KeyDown` leaves a binding of Escape unfired while the
  surface the keyboard is in claims Escape, and the surface's own branch takes the key. `Back` fires the binding
  itself, as it did.

The parts it reaches: the vocabulary (`ICodeSurfaceModel`, `ISheetSurfaceModel`), the code engine and the sheet
engine and their transpiled twins, the runtime's shortcut controller and text door, and Photon's host. The public
surface grows by the six `ClaimsEscape` members; nothing leaves it, and the developer surface does not move.
