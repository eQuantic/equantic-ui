# Proposal

Closes #1127, the first slice (E3a) of #1093 (E3), which is a sub-issue of #937 and of the 1.0 epic
#898. The design it follows is `docs/ELEMENT-TREE-PLAN.md`, section 3.9: the engine is written once,
in C#, and transpiled into the runtime like the code editor's and the spreadsheet's.

## Why

The browser keeps no element tree. Its instance stores, module maps and path attributes answer
"which instance is at this position" a second time, in hand-written TypeScript that already differs
from the C# engine (a store per host against one per page and island, an event against a hook). E3
moves the browser onto the C# engine, and E3a is the slice that gets the engine INTO the runtime:
until it transpiles, nothing in the browser can drive it.

E1 left the engine `[ServerOnly]` and measured what eqc refuses in it. Measured again on 2026-11-09,
with `[ServerOnly]` lifted, on main after #1202:

- `ChildSlots`: 43 errors EQ1007. It is the vocabulary's visitor, `IVisualNodeVisitor`, one `Visit`
  overload per node kind, and a JavaScript class has one member per name.
- `ComponentElement`: EQ2010. It builds a component through `ComponentBoundary`, a host-only static
  class whose report, diagnostics and contained list are three `AsyncLocal`s.
- `NodeElement`: EQ2004, the `GetType().Name` in one message.
- `Element`, `BuildOwner`, `NodeSlots`: build. `canUpdate`'s `GetType() == GetType()` translates since
  #1202.

## What Changes

- `Primitives/Elements` leaves `[ServerOnly]` and joins the model sources
  `SharedComponentTranspilationTests` transpiles: its modules are committed in the runtime beside the
  two other engines, pinned byte for byte, and executed by the runtime's own suite. Nothing in the
  browser drives them yet (E3b does).
- `ChildSlots` becomes one switch over the node's type, whose default throws an exception that names
  the type. The guarantee the visitor gave, that a node kind added to the vocabulary cannot be
  forgotten, moves to a test that EXECUTES: it mounts one instance of every concrete `VisualNode` in
  Primitives through the owner, built by reflection, and a kind with no arm fails it by name.
- A component builds through its owner. `BuildOwner` is handed the way a component is built (the
  error boundary, its report and its depth bound) by whoever makes it, and `ComponentElement` asks
  the owner instead of naming `ComponentBoundary`. Photon and the server hand it
  `ComponentBoundary`'s, so nothing they do changes. This is the start of plan 3.6, "the error
  boundary's report and tally" being the owner's.
- `NodeElement`'s message names the node's `NodeKind` instead of its .NET type name.

For a developer using the SDK nothing changes: no public signature an app calls moves (plan 4: "E1 to
E4 change no public signature an app calls"). The runtime bundle grows by the engine's modules, which
the served-runtime budget measures.

Parts reached: `eQuantic.UI.Primitives` (the engine), the runtime (the transpiled modules and a
spec over them), the web tests (the shared sources and the coverage test), the conformance suite, and
the served-runtime budget. eqc is not changed. The public surface of Primitives moves where
`BuildOwner` takes the build: host plumbing, declared in `PublicAPI.Unshipped.txt`, called by no
sample. The developer surface does not move.
