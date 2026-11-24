- **2026-11-16 · A component that builds itself is contained in a host too**: a component whose `Build` returns
  itself, or a box around itself, took the server's process down with a stack overflow when a `MarkupHost` drew it,
  and froze the browser's tab in a loop, where the lowering with no host contained it. The engine had contained the
  chain at the boundary's bound; the lowering found a component by its instance alone and met the outermost position
  of the chain at every step. Both lowerings now find a component inside the position whose build they lower, as
  Photon's layout already walks positions, so the chain draws the boundary's surface once and its siblings are drawn
  ([#1544](https://github.com/eQuantic/equantic-ui/issues/1544)). Proposed and archived through OpenSpec
  (`openspec/specs/element-tree`).
