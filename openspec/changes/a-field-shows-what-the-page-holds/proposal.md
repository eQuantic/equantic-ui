# Proposal

Closes #1026: a `TextEntry` on the web kept showing what was typed after the page reset it.

## Why

Found on eQuantic.Auth's hosted sign-in: a refused code was answered with `SetState(() => _code = "")`,
and the field kept showing the digits, with the page holding an empty one. In the browser,
`input.getAttribute('value')` was `""` and `input.value` was `"111111"`. The reconciler compares one tree
with the last one and never reads the element, and `SetState` folds into one render a frame, so when the
typing and the reset landed in the same flush the page held `""` both times and nothing was written. A
multiline field was worse: its value is the textarea's child text, and once typed in, no reset ever
showed.

## What Changes

The reconciler compares a text control's LIVE value with what the page holds, after updating its
attributes and children, and writes it when they differ, as React's controlled rule does. It does so
only for a control with an `input` handler, because one without has no page behind it and stays what
the person made it.

For a developer: `SetState(() => _code = "")` clears the field, every time, on both kinds of field.
Nothing changes on Photon, whose fields read the node each frame. No public surface moves.

## Capabilities

### Modified Capabilities

- `text-entry`: a field with a page behind it shows what the page holds.

## Impact

- `src/eQuantic.UI.Runtime/src/dom/reconciler.ts`.
