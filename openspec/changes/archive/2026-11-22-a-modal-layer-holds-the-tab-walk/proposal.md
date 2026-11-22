## Why

Closes #1066, a bug under #849. On Photon a modal `Overlay` (a dialog, a sheet, a
drawer) did not hold the keyboard: Tab and Shift+Tab walked every focus stop of the frame in paint order, the
ones behind the layer included. In a dialog over a page with a button, Tab from the dialog's last control
landed on the page's button behind the scrim, where the web's focus trap (`dom/focus-trap.ts`) cycles to the
dialog's first control. Measured writing the spreadsheet's keyboard exit (#983, #732): a page
`Stack[Button("Behind"), Overlay(Column[Button("First"), Spreadsheet])]`, a press on the sheet, Escape then
Tab, and the host's focus was `Behind`.

The `modal-focus` spec says how a trap shares Tab with the focused control, and its scenarios were proved in
the browser only.

## What Changes

For a developer using the SDK, nothing to write: while a modal and open layer is shown on Photon, Tab and
Shift+Tab walk the controls of the topmost such layer only and wrap inside it, as the web's trap cycles. A
layer opened from inside it (a menu in a dialog) is part of its walk; a toast is not. A modal layer with no
control holds the keyboard all the same, and nothing behind it takes the focus. The control that has the focus
still answers Tab first, so a code editor in a dialog indents.

- **The frame names its trap.** `RealizeResult` answers where the topmost modal and open layer was built, gated
  on modal and open exactly as the semantics walk announces a dialog.
- **The host walks inside it.** `PhotonHost`'s focus walk skips every stop that is not built under that layer,
  and wraps among the rest. From a stop behind the layer, Tab reaches the layer's first control and Shift+Tab
  its last, as the web's trap moves a focus it finds outside.

The parts it reaches: Photon's realizer result and host, internal members only. The public surface and the
developer surface do not move. The web is unchanged: its trap already holds the walk.
