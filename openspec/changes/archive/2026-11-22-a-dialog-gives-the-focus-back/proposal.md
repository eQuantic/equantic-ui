# Proposal

Closes #1811, a Bug under #1587. The pull request also carries #1066 and #1181, whose changes are
archived beside this one.

## Why

A modal dialog should give the focus back to what opened it when it closes. When that control no
longer exists, the focus should go to a logical control in its place, as WAI-ARIA's dialog pattern asks.

- **The web** recorded the opener and gave the focus back to it. When the opener had left the page,
  the trap gave up and the focus fell to the document's body. That happens for a confirm dialog that
  deletes the row whose button opened it, and the keyboard user is back at the top of the page.
- **Photon** recorded nothing. A modal layer that opened did not take the focus, and one that closed
  did not give it back. The focus stayed on the button that closed the dialog, and left with it (#976).

A reader on r/csharp asked for exactly this as a side-by-side demo: the focus back on the button that
opened the dialog, and on the next row when the dialog deleted the row that opened it.

## What Changes

- **On both targets**, a modal layer that closes gives the focus back to the control that had it when
  the layer opened, however the layer closed.
- **When that control is gone**, the focus goes to the stop that now stands at its place, among the
  ones Tab reaches beneath the layer. That is the same control of the next row, or the last one when
  it was the last row. Beneath a dialog that stays open, that place is inside the dialog, never on the
  page.
  - This is the one case where a control inherits the focus of another that left. #976 keeps every
    other case from it.
- **On Photon**, a modal layer that opens takes the focus: on the control marked
  `Pressable.InitialFocus`, else on its first stop, unless an Autofocus inside already took it. The ring
  stays as it was, so it shows only when the keyboard opened the layer.

No public member moves, and no developer surface. On Photon, the walk of a dialog open from the start
now begins on its first control, where it used to begin behind the layer.

## Principles

One contract with two realizations, the same scenario proved on each (docs/PRINCIPLES.md). The web's
trap was the reference: Photon now does what it does, and both do what neither did, which is the case
of the opener that is gone.
