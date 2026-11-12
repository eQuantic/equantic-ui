- **2026-11-12 · A gesture on the web reads the space its surface's offset moves in**: three Bugs under #166
  ([#1195](https://github.com/eQuantic/equantic-ui/issues/1195), [#1204](https://github.com/eQuantic/equantic-ui/issues/1204),
  [#1492](https://github.com/eQuantic/equantic-ui/issues/1492)). The web read a drag and a dismiss on the screen, where
  Photon reads them in the surface's own space since #734. Measured in Chromium 145: a horizontal track in a box turned
  a quarter heard nothing along itself, a track turned by its own transform jumped to its end, a row under a parent at
  `scale(2)` was drawn 80px away for a 40px move, and a sheet under the same scale was dismissed by 120px on the screen.
  Both controllers now read every point through the inverse of the ancestors' transforms the press found, keep the
  slop on the screen, and a normalized drag divides by its untransformed box. Photon's own case of a surface turned by
  its own transform is pinned beside them. Proposed and archived through OpenSpec (`openspec/specs/gestures`,
  `openspec/specs/transformed-surfaces`).
