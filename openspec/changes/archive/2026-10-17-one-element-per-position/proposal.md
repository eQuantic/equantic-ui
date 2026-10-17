# Proposal

Closes #950, the first slice (E1) of #937, which is a sub-issue of the 1.0 epic #898. The design it
follows is `docs/ELEMENT-TREE-PLAN.md`, sections 3.1, 3.2, 3.6 and 3.9.

## Why

On Photon a stateful component is found again every frame by a string, `"{path}#{type}#{key}"`, in
`ComponentInstanceStore`, and `Build` runs inside measure, once to three times a frame, for the whole
tree, on every frame the host draws: a frame that only moves a skeleton's shimmer rebuilds every
component on the screen. The string model also costs a defect today: a stateful root gains one
`StateInvalidated` handler on every frame (#935, 101 handlers after 100 frames), because the store
reports an instance it already holds as entering retention.

The plan decided on 2026-10-14 that the Element is built before 1.0, because the context every
`Build` receives becomes a position, and changing that after the cut costs a major. E1 is the slice
that gives Photon its element tree and its build phase, on which E2 to E5 build.

## What Changes

- An element tree in `eQuantic.UI.Primitives`: `Element`, `ComponentElement` (a component's
  position, which retains a `StatefulComponent` instance), `NodeElement` (every other node's
  position, holding its children by slot), and `BuildOwner` (the tree's dirty list, its build phase,
  and the order of the lifecycle hooks). Host plumbing, `[ServerOnly]`: an app never names any of
  them.
- Photon keeps one element tree per host across frames. A frame BUILDS first, then lays out and
  paints: the layout measures what the elements built and builds nothing, so a container that
  measures a child twice, and intrinsic sizing, read one built subtree, and a stateful child is
  measured with its own state.
- A `SetState` marks its component's element dirty and asks the host for a frame. A frame the host
  draws only for motion (a glide, a transition, a presence, a loop, the frame ticker) builds nothing;
  a frame after input or an invalidation rebuilds every component, as every frame does today.
- An overlay's child and an anchored panel are built when their layer is realized, and a layer that
  is not realized unmounts what it held, as today.
- Fixes #935 by construction: the host listens to its owner once, and no instance subscribes to
  anything.
- **BREAKING** (host plumbing, none of it called in `samples/`): `ComponentInstanceStore` is gone;
  `StatefulComponent.StateInvalidated` is gone; `NotifyMounted` and `NotifyUnmounted` are no longer
  public (the owner calls them); `PhotonRealizer.Realize` takes a `BuildOwner` where it took a
  `ComponentInstanceStore`; `LayoutContext.Instances` is gone. The migration line: a host that drove
  the store passes a `BuildOwner` instead, and a test that mounted a component by hand mounts it in a
  `PhotonHost` or a `BuildOwner`.

What does not change for a developer: `Build(ComponentContext context)`, `SetState`, `OnMount`,
`OnUnmount`, `AdoptConfig`, `Key` and every member of `ComponentContext`. The browser and the server
keep their own instance handling until E3 and E4, and the element engine stays host-only until E3
transpiles it (the measurement of what eqc refuses in it is recorded in design.md).

## Capabilities

### New Capabilities
- `element-tree`: how a live tree keeps a component's instance by its position, when it builds, and
  in what order its components mount and unmount.

### Modified Capabilities

## Impact

- `eQuantic.UI.Primitives`: the engine (new, `[ServerOnly]`), `StatefulComponent` (its element
  back-reference replaces `StateInvalidated`), `ComponentInstanceStore` removed. Public surface
  moves: `*REMOVED*` lines for the store, `StateInvalidated`, `NotifyMounted`, `NotifyUnmounted`;
  additions for the engine.
- `eQuantic.UI.Native.Framework`: the measure walk carries the element beside the path,
  `MeasureComponent` and intrinsic sizing read the built subtree, `LayoutEngine.Layout` takes the root
  element (a one-shot layout builds its own, structurally), `LayoutNode.Element` is new.
- `eQuantic.UI.Native.Components`: `PhotonRealizer` runs the build phase and the overlay pass over
  elements; `PhotonHost` owns a `BuildOwner`.
- Not reached: eqc, the runtime, the web realizer, the server, the SDKs, the templates, the
  developer surface (`developer-surface.baseline.txt`).
