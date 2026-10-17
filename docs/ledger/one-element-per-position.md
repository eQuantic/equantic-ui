- **2026-10-17 · Photon keeps an element per position, and a frame builds what changed**: E1 of the
  element tree (docs/ELEMENT-TREE-PLAN.md). `Element`, `ComponentElement`, `NodeElement` and
  `BuildOwner` are written in `eQuantic.UI.Primitives`, host-only until the browser drives them, and a
  Photon host keeps its tree in one across frames: a stateful instance is held by its position, matched
  as the paths always matched it, and `ComponentInstanceStore` is gone. A frame builds before it lays
  out, so a box that measures its child twice and intrinsic sizing read one subtree, with the child's
  own state, and a grid's hug column stopped probing every candidate at one shared path. A `SetState`
  marks its element, the owner asks the host for a frame, and only marked positions build; input and
  invalidations still rebuild everything. The host listens to its owner once, which closes the
  handler that a stateful root gained on every frame ([#935](https://github.com/eQuantic/equantic-ui/issues/935)),
  and a request for a frame made after the build is no longer written over at the frame's end. The
  dense scene's steady frame went from 64.2 KB to 38.3 KB and a stateful one's from 98,480 to 28,742
  bytes; the budgets are tightened to match. Run through eqc, the engine is refused in three places,
  recorded for E3 ([#950](https://github.com/eQuantic/equantic-ui/issues/950)). Proposed and archived
  through OpenSpec (`openspec/specs/element-tree`).
