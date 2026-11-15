# Proposal

Closes #1026 and #1092, two Bugs in what the web lost of where a person was: a `TextEntry` kept showing
what was typed after the page reset it (#1026), and a resize grip drawn inside a spreadsheet took the
keyboard away from it (#1092).

## Why

Found on eQuantic.Auth's hosted sign-in: a refused code was answered with `SetState(() => _code = "")`,
and the field kept showing the digits, with the page holding an empty one. In the browser,
`input.getAttribute('value')` was `""` and `input.value` was `"111111"`. The reconciler compares one tree
with the last one and never reads the element, and `SetState` folds into one render a frame, so when the
typing and the reset landed in the same flush the page held `""` both times and nothing was written. A
multiline field was worse: its value is the textarea's child text, and once typed in, no reset ever
showed.

A spreadsheet's header grip is a `Draggable` the sheet steps aside for, and the browser moved the focus
to the page's body on the press's `mousedown`, the grip's nearest focusable ancestor. The column resized,
but ⌘Z no longer reached the sheet, so the resize, one undo step, could not be taken back, and the
selection was drawn as a grid without the keyboard. On Photon a press inside a sheet makes it the
editing surface before anything drawn there takes the press, so the keyboard stays.

## What Changes

The reconciler compares a text control's LIVE value with what the page holds, after updating its
attributes and children, and writes it when they differ, as React's controlled rule does. It does so
only for a control with an `input` handler, because one without has no page behind it and stays what
the person made it.

A press on a resize grip inside a sheet keeps the keyboard on the sheet: the sheet's text door takes it
on the `pointerdown`, and the `mousedown` that would move it to the body is prevented, as the sheet's own
presses already prevent it.

For a developer: `SetState(() => _code = "")` clears the field, every time, on both kinds of field, and
⌘Z after resizing a column takes the resize back.
Nothing changes on Photon, whose fields read the node each frame. No public surface moves.

## Capabilities

### Modified Capabilities

- `text-entry`: a field with a page behind it shows what the page holds.
- `spreadsheet`: a resize keeps the keyboard on the sheet.

## Impact

- `src/eQuantic.UI.Runtime/src/dom/reconciler.ts` and the sheet surface's lowering in
  `src/eQuantic.UI.Runtime/src/shared/lowering.ts`.
