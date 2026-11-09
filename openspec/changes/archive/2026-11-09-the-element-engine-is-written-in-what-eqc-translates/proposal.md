# Proposal

Part of #1127, its first slice (E3a-1), in #1093 (E3), a sub-issue of #937 and of the 1.0 epic #898.
The design it follows is `docs/ELEMENT-TREE-PLAN.md`, section 3.9: the engine is written once, in C#,
and transpiled into the runtime like the code editor's and the spreadsheet's.

## Why

The browser keeps no element tree. Its instance stores, module maps and path attributes answer
"which instance is at this position" a second time, in hand-written TypeScript that already differs
from the C# engine. E3 moves the browser onto the C# engine, and E3a gets the engine INTO the runtime:
until it transpiles, nothing in the browser can drive it.

E1 left the engine `[ServerOnly]`. Measured on 2026-11-09, with `[ServerOnly]` lifted, eqc refused it
in four places:

- `ChildSlots`: 43 errors EQ1007. It was the vocabulary's visitor, `IVisualNodeVisitor`, one `Visit`
  overload per node kind, and a JavaScript class has one member per name. With the visitor gone, a
  fifth refusal showed in the same file: `Array.Empty<T>()` has no translation (EQ2004).
- `ComponentElement`: EQ2010. It built a component through `ComponentBoundary`, a host-only static
  class whose report, diagnostics and contained list are three `AsyncLocal`s.
- `NodeElement`: EQ2004, the `GetType().Name` in one message.

Closed, the engine goes through eqc with no diagnostic, and the modules eqc writes then fail the
runtime's `tsc` (55 errors) for two reasons no earlier module exercised: six emission defects of eqc's
(filed as bugs under #1174: #1299, #1303, #1304, #1305, #1306, #1307), and a browser component model
that differs from the C# one the engine is written against. #1127 is therefore split: this slice
writes the engine in what eqc translates and pins that it stays so; E3a-3 (#1301) brings the browser's
component to what the engine drives and puts the modules in the runtime once the bugs land.

## What Changes

- `ChildSlots` becomes one switch over the node's `NodeKind`, a typed read per arm, whose default
  throws an exception that names the kind. The guarantee the visitor gave, that a node kind added to
  the vocabulary cannot be forgotten, moves to a test that EXECUTES: it mounts one instance of every
  concrete `VisualNode` in Primitives through an owner, built by reflection, and a kind with no arm
  fails it by name.
- A component builds through its owner. `BuildOwner` is handed the way a component is built by whoever
  makes it, and `ComponentElement` asks the owner instead of naming `ComponentBoundary`.
  `ComponentBoundary.Owner()` and `StructuralOwner()` hand it the boundary every .NET target builds
  through, with its containment, report and depth bound, so Photon and the server build exactly as
  before. This is the start of plan 3.6, "the error boundary's report and tally" being the owner's.
- `NodeElement`'s message names the node's `NodeKind` instead of its .NET type name, and the code
  surface's slots are written without `Array.Empty<T>()`.
- A test passes the engine through eqc with its marks lifted, and fails on any refusal, any warning,
  or a file that yields no module. The engine stays `[ServerOnly]` until E3a-3, and a server-only type
  compiles to nothing, so without this test a construct eqc cannot translate would go unseen until
  then.

For a developer using the SDK one line moves, in a test: an app's test that mounts a component
through an owner, as the wiki's Capabilities page shows, makes it with `ComponentBoundary.Owner()`
where it wrote `new BuildOwner()`. It is the one place plan 4's "E1 to E4 change no public signature
an app calls" does not hold, because an owner that builds nothing is not one a test can be handed; the
page changes in all five languages in the same step. The rest of the public surface that moves is
host plumbing, declared in `PublicAPI.Unshipped.txt` and called by no sample. The developer surface
does not move, and neither does the runtime: no module is added to it.

Parts reached: `eQuantic.UI.Primitives` (the engine and `ComponentBoundary`), Photon's host, realizer
and layout engine (the owners they make), and the web and native tests. eqc is not changed.
