## 1. The server's lowering

- [x] 1.1 `ElementFrame` indexes a component's position by its instance and the component position above it, and `At` looks inside the entered position first
- [x] 1.2 `WebLoweringVisitor` enters a position for what it built, in `Visit(UiComponent)` and through a stack's `ResolveForPositioning`
- [x] 1.3 `ComponentBoundaryTests` run every case with no host, through `MarkupHost.Structural` and through `MarkupHost.ForPage`, with a component that builds a box around itself and one instance placed twice side by side
- [x] 1.4 Proved both ways: without the fix the host's cases crash the test host on a stack overflow; with it all pass

## 2. The browser's lowering

- [x] 2.1 `element-frame.ts` indexes and looks up as the C# frame does, with `within` and `withinParentOf`
- [x] 2.2 `lowering.ts` enters a position for a component's build, a stack's child and a record lowered again
- [x] 2.3 `component-boundary.spec.ts` contains a component that builds itself, one that builds a box around itself, both through a stack, and reports the chain once
- [x] 2.4 Proved both ways: without the fix the spec hangs; with it all pass

## 3. Photon and the documentation

- [x] 3.1 Photon's layout already follows positions: `Native.Engine.Tests/ComponentBoundaryTests` lay out a component that builds itself through an owner and pass unchanged
- [x] 3.2 One ledger entry under `docs/ledger/`
