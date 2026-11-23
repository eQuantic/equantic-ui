# A camera preview gets its stream in the render that shows it

Closes #1830, a bug under #1587, found reviewing #1812.

## Why

On the web, the camera streams attached at the end of a pass (`exitPass`), before the render manager writes the tree
the pass produced. A preview a render showed had no `<video>` in the document yet, so it played nothing until some
later pass came, and a page that came to rest showed a black preview. #1812 found the same order for the modal focus
traps and moved their reconciliation to the microtask after the write.

## What changes

- One moment after the write, `shared/after-write.ts`, runs what the end of a pass does against the written document:
  the focus traps, which #1812 moved there with a microtask of their own, and the camera streams. A task queued twice
  before the flush runs once, and a focus-trap reset drops a reconciliation still queued.
- `installHoverRevealSuppression` stays at the end of the pass: it installs a document listener once and reads nothing
  a pass writes.

For a developer nothing is written differently: a preview shows its camera in the render that shows the preview. No
public surface and no developer surface moves.

This builds on #1812's change to `element-roots`' `exitPass`, and lands after it.

## Principles

It follows 9 (the structural fix: one moment that reads what a pass wrote, rather than a second scheduling of its own)
and 4 (the web realizes what Photon draws in the frame the node is emitted in).
