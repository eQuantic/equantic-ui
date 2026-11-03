# Proposal

Closes #702 and #734, Bugs under #166 (Geometry and semantics live in the vocabulary): a gesture on a
surface drawn under a transform measured on the screen, on each target, while what it writes lives in
the surface's own space.

## Why

- On Photon (#734), since #690 a draggable and a scroll view under a transform take the pointer where
  they are drawn, and then measured the gesture on the screen. A 40dp drag of a surface drawn at 2×
  stored a 40dp translation, so its drawing moved 80. A horizontal draggable or a vertical list turned
  a quarter heard nothing along the line it is drawn on. A pan wrote the screen distance into the
  scroll offset and the fling's speed.
- On the web (#702), a normalized `Draggable` that follows divided by `getBoundingClientRect()`, which
  includes the surface's own `transform`, while its rest is a `translate` percentage of the
  untransformed box. Measured in Chrome 152: a 200px surface at `scale(.5)` with a rest of 0.25 sits at
  x 400, and a finger moving 14px took it to 389, then 415 after 40px.

## What Changes

- On Photon a captured gesture reads every point in the space its surface was laid out in, through the
  inverse the frame keeps, the one the press found, held for the whole gesture: a drag's travel, its
  normalization by the local extent, its paint offset and its release, a dismiss's threshold, a pan's
  positions and the fling's speed.
- On the web a normalized gesture divides by the rect without the surface's own scale, so what scales
  the page around the surface stays in it, and writes its fraction back in the box a percentage
  resolves against.

For a developer: a drag or a pan under a `Transform` follows the finger on both targets, and reports
the same value it would unscaled. No public surface moves.

## Capabilities

### Modified Capabilities

- `transformed-surfaces`: a captured gesture is read in the surface's own space.
- `gestures`: a normalized drag on the web is read against the box its rest is written in.

## Impact

- `src/eQuantic.UI.Native.Components/PhotonHost.cs` and `src/eQuantic.UI.Runtime/src/dom/draggable.ts`.
