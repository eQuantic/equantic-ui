## Context

The web realizes a code surface as a textarea whose `keydown` hands the key to the engine's keymap,
and prevents it when the keymap claims it. A modal layer's focus trap listened for Tab on the window
in the capture phase, so it ran before that handler. On Photon, `PhotonHost.KeyDown` asks the sheet
target and the code target before it cycles the focus.

The code document normalizes line breaks on the way in (`CodeDocument.FromText`), and the controller
recorded the raw text in the `CodeEdit`, whose `InsertedRange` split on LF alone.

## How Flutter answers it

- A key event goes to the primary focus first and bubbles up the focus tree, each `Focus.onKeyEvent`
  answering `handled` or `ignored`. Focus traversal (`NextFocusIntent`, bound to Tab by the app's
  default `Shortcuts`) runs only for a key nothing below it handled, so an editable that handles Tab
  keeps it inside a modal route as anywhere else.
- `UndoHistory` keeps the `TextEditingValue` the controller holds, not the input that produced it.

## Decisions

- **The trap hears Tab on its way back up.** The window's `keydown` listener moves from the capture
  phase to the bubble phase and skips a Tab whose default is already prevented: the focused control
  answers first, as Flutter's focus tree and Photon's host do. No registry of Tab-keeping elements and
  no attribute on the editor: a control says it kept the key the way the platform already lets it,
  by preventing it.
- **An edit carries the text the document holds.** The controller records the inserted text read
  back from the new document (`TextIn` over the range it now occupies), so the history and every
  listener see one shape of line break.
- **`CodeEdit` breaks its lines where the document does.** `InsertedRange` counts lines through
  `CodeDocument.FromText`, and `IsSimpleInsert` treats a CR as a break too, so an edit a host builds
  by hand with a lone CR measures what the document will hold.

## Fences

- No control in the SDK stops a Tab keydown's propagation, which would now hide it from the trap; the
  `focusin` guard still pulls the focus back into the layer if one ever did.
