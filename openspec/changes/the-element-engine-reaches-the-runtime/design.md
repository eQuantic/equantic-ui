## Context

`docs/ELEMENT-TREE-PLAN.md` 3.9: the engine is written once in C# and transpiled into the runtime, the
realizers drive it through a narrow interface, and the first slice closes what stops eqc from
transpiling it. E1 (#950) built the engine for Photon and fenced it `[ServerOnly]`. The parity row it
serves is `docs/FLUTTER-PARITY.md`'s `Element` tree, `BuildContext`, `BuildOwner` (PARTIAL, "the
structural one"). E3a does not flip it: the row moves when the browser drives the engine (E3b, E3c).

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

Plan 3.6 makes the error boundary's report and tally the owner's. E3a takes the first step: the owner
is handed how a component builds, and `ComponentElement` asks the owner. The wiring that hands it
`ComponentBoundary`'s build stays with `ComponentBoundary`, which stays `[ServerOnly]`, so the engine
names neither and Photon and the server build exactly as they do today. Moving the three
`AsyncLocal`s onto the owner is E4's (plan 3.5, "the Primitives ones the tree owns").

### A message names the node's kind

`NodeElement`'s "has no slot the realizer shows" message names `NodeKind` instead of the .NET type's
name, which the browser does not keep for a value of every type (EQ2004).

### The same cases run on both sides

The engine's C# behaviour is `ElementTreeTests` (27 cases, driven through the owner with no host). The
transpiled engine is proved the way this repository proves any translation: the cases are written once
as conformance snippets, executed on .NET and transpiled and executed in JavaScript against the runtime,
and their answers compared. The pinned modules hold what the runtime carries byte for byte.

## Risks / Trade-offs

- [The runtime bundle grows by the engine] → measured against `served-runtime.json`; the budget moves
  with the number in the pull request, not silently.
- [Photon's build reads every node's slots] → the dense scene's and the stateful dashboard's frame
  measured before and after, as E1 and E2 were.
- [The conformance harness reaching runtime-provided engine types] → measured first, on one case,
  before the cases are written.
- [T1 (#1245) changes `Element`, `NodeElement` and `ComponentElement` beside this] → main merged in when
  it lands. Its `VisitChildren` is one override per kind and no overload, so it transpiles.
- [A generic component built through its factory carries no closure mark] → two such components of
  different closed types compare as one type in `canUpdate` (#1255's review). Measured in E3b, where the
  browser first drives `canUpdate`; marking at the factory's call site, where C# knows the closed type,
  is the candidate.
