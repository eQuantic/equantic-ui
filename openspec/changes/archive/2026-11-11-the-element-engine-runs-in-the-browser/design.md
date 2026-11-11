## Context

`docs/ELEMENT-TREE-PLAN.md` 3.9 writes the engine once and transpiles it. E3a-1 (#1314) closed what eqc
refused and kept the engine `[ServerOnly]` behind a gate test; the component-as-a-node slice (#1394) gave
the browser a component that is a node. This slice lifts the mark. How Flutter answers: there is one
framework, and the web build runs the same `Element` code as every other target.

## Decisions

### The engine is pinned and checked like the other engines

`Primitives/Elements` joins `SharedComponentTranspilationTests`' model sources; its modules are written
beside the code editor's and the spreadsheet's and exported by the barrel. They are type-checked by the
runtime's `tsc` with everything else. While eqc wrote them, it met one more emission defect, filed under
the bug sweep (#1396): a downcast to a type whose member is inherited loses its type.

### The browser's components carry what the engine calls

The engine calls, on a component, what C#'s `UiComponent` and `StatefulComponent` have: `AdoptConfig`
(a default that copies nothing), the position a stateful instance is mounted at, and taking and leaving a
position. The browser's twins gain them with C#'s semantics, and the stateful component's `SetState` marks
its positions beside the render it schedules today, which is a no-op until something mounts it (E3b).

### One namespace per class

C#'s `Place` and `Leave` are internal, so an app's component could declare its own and C# would keep them
apart. A JavaScript class cannot: the library's `NameBox` and `TableView` already had a private `Place`,
which the engine would have called. The members take names an app is unlikely to write
(`EnterPosition`, `LeavePosition`), and eqc's list of runtime members, read from the live prototypes,
refuses the rest.

### A base imported from its own module

Every library module imports the library through `runtime-exports`, which re-exports the barrel. The
module runner defines a re-export only once the module it names has loaded, so a class evaluated while
the barrel loads reads a sibling base as `undefined`. The writer of the embedded modules imports a
sibling base from its own module; the code editor's languages already did, since eqc imports a type of
their own engine from its module.

### The same cases on both sides

The cases are `ElementTreeTests`', written again as scenarios that run on .NET and on the transpiled
engine with twin components and compare traces. The browser's owner is handed the plain build, since the
browser's boundary stays the lowering's until E3b; no case throws.

## Risks / Trade-offs

- [The served runtime grows by the engine] → measured and recorded in the budget with its number.
- [`SetState` now marks positions in the browser] → nothing mounts a browser component at a position yet,
  so `mountedAt` is null and the mark does nothing until E3b; the runtime's suite is unchanged.
