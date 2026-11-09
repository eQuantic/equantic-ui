## Context

`docs/ELEMENT-TREE-PLAN.md` 3.9: the engine is written once in C# and transpiled into the runtime, the
realizers drive it through a narrow interface, and the first slice closes what stops eqc from
transpiling it. E1 (#950) built the engine for Photon and fenced it `[ServerOnly]`. The parity row it
serves is `docs/FLUTTER-PARITY.md`'s `Element` tree, `BuildContext`, `BuildOwner` (PARTIAL, "the
structural one"). This slice does not flip it: the row moves when the browser drives the engine (E3b,
E3c).

How Flutter answers each question below, first, as the house rules ask.

## Decisions

### A node's children are read by its kind, in one switch, and a test proves every kind has an arm

Flutter puts the knowledge in the widget's own class: a `SingleChildRenderObjectWidget` has a `child`,
a `MultiChildRenderObjectWidget` has `children`, and each `Element` subclass implements
`visitChildren`. Here the vocabulary is data shared by every target and carries no engine code, so the
engine reads the slots from outside, in `ChildSlots`.

`ChildSlots` is an `IVisualNodeVisitor` today, so that a node kind added to the vocabulary fails the
build until it has a door. eqc refuses it (EQ1007): a JavaScript class has one member per name, and the
visitor is 45 overloads of `Visit`. It becomes one `switch` over `node.NodeKind`, the wire
discriminator the browser already dispatches by (`node-kinds.generated.ts`), with a typed read of the
node in each arm and a default that throws an exception naming the kind. A string switch is a hash
jump in .NET and a `switch` in JavaScript, where a switch over 45 types would be up to 45 type tests
per node on every build Photon runs.

The guarantee moves to a test that EXECUTES rather than one that reads the source: it enumerates by
reflection every concrete `VisualNode` in Primitives, builds one instance of each with
`FactoryArguments` (the web tests' total argument builder), mounts it through `BuildOwner` (ChildSlots
is internal, so the public owner is the way in) and requires that the default never fires. A kind
`FactoryArguments` cannot build goes in a baseline that may only shrink, after
`ir-migration.baseline.txt`. A kind added with no arm fails the test with its name. Two kinds that
share a `NodeKind` fail it too, on the typed read.

Rejected: teaching eqc to lower a visitor, with an `accept` on the ~45 hand-written classes of the
runtime's `vocabulary.ts`. It keeps the guarantee in the compiler, but in the wrong place: that file is
the one #163 means to generate. And a hand-written TypeScript `ChildSlots`, which 3.9 refuses.

### A component builds through its owner

Flutter catches a build's exception in `ComponentElement.performRebuild`, shows `ErrorWidget`, and
reports through the static `FlutterError.onError`. The engine here does the same through
`ComponentBoundary`, a static class whose report, diagnostics and contained list are `AsyncLocal`s,
because many server requests render at once on shared statics. It is host-only, and `ComponentElement`
naming it is the EQ2010 that keeps the engine out of the runtime.

Plan 3.6 makes the error boundary's report and tally the owner's. This slice takes the first step:
the owner is handed how a component builds, as Flutter's owner is handed `onBuildScheduled`, and
`ComponentElement` asks the owner. The build is a `Func<UiComponent, ComponentContext, int,
VisualNode>` (the component, its context, its depth of components) taken by the constructor and by
`Structural`, with no parameterless constructor left: an owner that forgot it would build nothing. The
wiring that hands it `ComponentBoundary`'s build is two factories on `ComponentBoundary`, `Owner()` and
`StructuralOwner()`, which stays `[ServerOnly]`, so the engine names neither and Photon and the server
build exactly as they do today. Every place that made an owner, in Photon and in the tests, calls a
factory. Moving the three `AsyncLocal`s onto the owner is E4's (plan 3.5, "the Primitives ones the tree
owns").

Rejected: a named delegate type (no `public delegate` crosses to the browser), and suppressing
CA2255 to register the boundary's build from a module initializer, which would have kept the
parameterless constructor at the price of a hidden global.

### A message names the node's kind

`NodeElement`'s "has no slot the realizer shows" message names `NodeKind` instead of the .NET type's
name, which the browser does not keep for a value of every type (EQ2004).

### The engine stays `[ServerOnly]`, and a test holds it to what eqc translates

With every refusal closed, the engine's modules were generated as the other engines' are, and the
runtime's `tsc` refused them: 55 errors. Six are eqc's own emission defects, which no module before the
engine exercised, among them a silent one (a member named `Component` read bare is written
`this._component`, so a read from outside the class sees its default). The rest is the browser's
component model, which differs from the C# one: a component is not a `VisualNode` there, its stateful
component has no `MountedAt`, `Place` or `Leave`, and `InFlow` has no `Current`.

Committing modules that fail the runtime's own type check is not an option, and fixing the emission
belongs to the bug sweep. So the slice stops short of the runtime: the defects are bugs under #1174
(#1299, #1303 to #1307), the browser's component is E3a-3 (#1301), and the engine keeps `[ServerOnly]`
until then. A server-only type compiles to nothing, so a construct eqc cannot translate written into
the engine meanwhile would surface only in E3a-3; a test closes that gap by passing the engine through
eqc with its marks lifted from the sources it hands the compiler, and failing on a refusal, a warning,
or a file with no module. The cases `ElementTreeTests` holds the C# engine to become conformance cases
in E3a-3, executed on both sides.

## Risks / Trade-offs

- [Photon's build reads every node's slots by a string switch instead of a virtual call] → measured on
  main and on this branch, Release, three runs each: allocation identical to the byte in every scene of
  the perf harness, the dense scene's realize p50 0.33 to 0.35 ms against 0.35 to 0.37 ms on main.
- [The engine is held to eqc only through a test while it stays `[ServerOnly]`] → the test reads the
  same folder E3a-3 hands `SharedComponentTranspilationTests`, so nothing in between can drift.
- [T1 (#1245) changes `Element`, `NodeElement` and `ComponentElement` beside this] → whichever merges
  second takes the other's owner creation sites onto the factories.
- [A generic component built through its factory carries no closure mark] → two such components of
  different closed types compare as one type in `canUpdate` (#1255's review). Measured in E3b, where the
  browser first drives `canUpdate`; marking at the factory's call site, where C# knows the closed type,
  is the candidate.
