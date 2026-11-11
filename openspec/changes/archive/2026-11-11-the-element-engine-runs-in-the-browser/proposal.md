# Proposal

Closes #1301 (E3a-3), the last slice of #1127 (E3a), in #1093 (E3), a sub-issue of #937 and of the 1.0
epic #898. The design is `docs/ELEMENT-TREE-PLAN.md` section 3.9: the engine is written once, in C#,
and transpiled into the runtime like the code editor's and the spreadsheet's.

## Why

E3a-1 (#1314) wrote the engine in what eqc translates and held it there with a test, leaving it
`[ServerOnly]`; the component-as-a-node slice (#1394) made every browser component a node, which the
engine's position needs. What was left is the engine itself in the runtime: its modules committed and
pinned, and the runtime holding it to the same answers as the C# engine. Nothing in the browser drives
it yet; E3b (#1128) moves the browser onto it.

## What Changes

- `Primitives/Elements` leaves `[ServerOnly]` and joins the model sources `SharedComponentTranspilationTests`
  transpiles: `BuildOwner`, `Element`, `ComponentElement`, `NodeElement`, `ChildSlots` and `NodeSlots`
  are committed in the runtime beside the other engines, pinned byte for byte, exported from the served
  runtime, and type-checked by its `tsc`.
- The browser's stateful component carries the position the engine mounts it at, as the C# one does
  (`MountedAt`), and its `SetState` marks those positions for the engine's next build beside today's
  scheduling. The C# `Place` and `Leave` become `EnterPosition` and `LeavePosition`: in JavaScript a
  class has one namespace, and `NameBox` and `TableView` each had a private `Place` that would have
  answered the engine in place of the base's. eqc's list of runtime members carries the new members, so
  an app method of one of those names is refused (EQ2011).
- The browser's `UiComponent` gains C#'s empty `AdoptConfig` default, which the engine calls on every
  instance it keeps; `InFlow` gains the static `Current` over the ambient the lowering already arms;
  `AdaptiveNode` gains C#'s `Resolve`; and a child slot is typed as the `VisualNode` it is in C#.
- A library module whose class extends a sibling (`ComponentElement` and `NodeElement` extend `Element`)
  imports the base from the sibling's module rather than through the runtime's aggregator, which defines
  a re-export only after the barrel has loaded.
- Eleven cases of `ElementTreeTests` run twice, on .NET and on the transpiled engine, and their traces
  are equal (`ElementEngineTwinTests`).

For someone using the SDK: nothing they write changes, except that a member of a stateful component
named `enterPosition`, `leavePosition`, `mountedAt` or `adoptConfig` other than an override is refused at
build time. The served runtime grows by the engine's modules, which its budget records.

Parts reached: `eQuantic.UI.Primitives` (the engine and `StatefulComponent`), the runtime (the generated
modules, `core/component.ts`, `shared/vocabulary.ts`), eqc's list of runtime members, the web tests (the
model sources and the embedded writer), the compiler tests and the conformance suite.
