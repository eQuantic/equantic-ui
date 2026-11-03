- **2026-10-30 · A gesture reads the space its surface was laid out in**: two Bugs under #166
  ([#702](https://github.com/eQuantic/equantic-ui/issues/702), [#734](https://github.com/eQuantic/equantic-ui/issues/734)).
  A surface under a transform took the pointer where it was drawn and then measured the gesture on the screen. On
  Photon a 40dp drag at 2× stored 40dp and was drawn 80 away, a track turned a quarter heard nothing along itself,
  and a pan wrote screen distances into the scroll offset and the fling. A captured gesture now reads every point
  through the inverse the press found, held until the release. On the web a normalized drag divided by a rect that
  includes the surface's own scale while its rest is a percentage of the untransformed box, so a surface at
  `scale(.5)` jumped back 11px from a 14px move (Chrome 152: 400, 389, 415); it stays under the finger now (400, 414,
  440). Proposed and archived through OpenSpec (`transformed-surfaces`, `gestures`).
