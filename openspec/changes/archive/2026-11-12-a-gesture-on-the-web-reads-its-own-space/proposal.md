# Proposal

Closes #1195, #1204 and #1492, Bugs under #166 (Geometry and semantics live in the vocabulary): on the
web a captured gesture under a transform was read on the screen, where Photon reads it in the space its
surface's offset moves in since #734.

## Why

- A horizontal `Draggable` inside a box turned a quarter is drawn down the screen. The web read the
  finger along `clientX`, so a drag along the drawn track heard nothing and a sideways move, across
  it, drove it (#1195). Measured in Chromium 145: 40px down the screen left it where it was.
- A normalized drag divided by the surface's rect, which under a quarter turn, its own or an
  ancestor's, is as wide as the track is thick: a 200px track turned by its own transform, dragged
  40px, jumped to its end and reported 1 (#1204).
- A drag that is not normalized wrote the screen's travel into `translate`, which resolves in the
  parent's space: under a parent drawn at `scale(2)` a row moved 80px for a 40px move of the finger
  and reported 40, where Photon reports 20 (#1204).
- A `DragDismiss` counted its threshold on the screen: under a parent at `scale(2)`, a sheet dragged
  120px was drawn 240px away and dismissed, where Photon counts 60 of the sheet's 96 (#1492).

## What Changes

- The web reads a drag's and a dismiss's every point through the inverse of the composed transforms
  of the surface's ancestors, the space its `translate` or `translateY` moves it in, read when the
  press lands and held until the release. The slop stays a distance on the screen.
- A normalized drag divides by the surface's untransformed box, the one a `translate` percentage
  resolves against, which is also what it writes its value back in. The rect is no longer read.
- The surface's own transform stays out of the space: it turns and scales what the surface draws, and
  the offset is applied outside it, as Photon paints a drag's offset outside its child's transform.

For a developer: a `Draggable` or a `DragDismiss` under a `Transform` follows the finger on the web as
it does on Photon, and reports the same value. No public surface and no developer surface moves.

## Capabilities

### Modified Capabilities

- `transformed-surfaces`: a captured gesture is read in the surface's own space on the web too.
- `gestures`: a normalized drag on the web divides by the box its rest is written in, under any
  transform.

## Impact

- The runtime only: `src/eQuantic.UI.Runtime/src/dom/gesture-space.ts` (new), `draggable.ts` and
  `drag-dismiss.ts`. eqc, the realizers' lowering, the Photon shells and the SDKs are untouched;
  `TransformedSurfaceTests` gains the case of a surface turned by its own transform, which Photon
  already answered, to pin the agreement.
- Principles: one behaviour on every target (the web agrees with Photon's measured answers), and the
  instrument fails, since every case is a spec that fails against the old controller.
