- **2026-11-09 · The element engine is written in what eqc translates**: the first part of E3a.
  `ChildSlots` reads a node's children by `NodeKind` in one switch, and a test that mounts one
  instance of every concrete `VisualNode` through an owner keeps the guarantee the visitor gave; the
  owner is handed how a component builds, so the engine no longer names the host-only
  `ComponentBoundary`, whose `Owner()` and `StructuralOwner()` now make every owner (an app's test
  that wrote `new BuildOwner()`, as the wiki's Capabilities page showed, writes `Owner()`); and the engine
  goes through eqc with no diagnostic, which `ElementEngineTranspilesTests` holds while it stays
  `[ServerOnly]`. Generated as the other engines are, its modules failed the runtime's `tsc`: six
  emission defects of eqc's, one of them silent and reaching app code (a member named `Component`),
  filed under the bug sweep (#1299, #1303 to #1307), and a browser component model that differs from
  the one the engine drives, which is E3a-3 (#1301). Photon's frame is unchanged, allocation identical
  to the byte in every scene ([#1127](https://github.com/eQuantic/equantic-ui/issues/1127)). Proposed
  and archived through OpenSpec, adding three requirements to `element-tree`.
