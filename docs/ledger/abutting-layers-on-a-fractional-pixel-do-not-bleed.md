- **2026-11-26 · A fill's edges land on whole device pixels on Photon, so a layer behind no longer shows through a
  shared edge**: two layers sharing an edge on a fractional device pixel each covered the same fraction of it, and
  the one behind survived as a hairline, a red line under a closed swipeable row on an Android emulator at 2.625
  device pixels a dp. A browser snaps a background's edges, and the web drew no line. `FillRRect` now snaps an
  axis-aligned fill's edges to whole device pixels, half up, leaving turned fills, discs and corners as they were, and
  `PushClip` snaps a clip the same way: the row draws both layers under its box's clip, and on the emulator the fill
  snap alone left the line, which the clip snap took away. 76
  goldens moved by their edge pixels only, each audited before it was regenerated
  ([#1344](https://github.com/eQuantic/equantic-ui/issues/1344)). Proposed and archived through OpenSpec
  (`openspec/specs/box-paint`).
