# Proposal

Closes #701, #736 and #738, Bugs under #166 (Geometry and semantics live in the vocabulary): the same
tree laid out differently depending on whether a component wraps a node, and a bridge's subtree
reading two themes.

## Why

- On Photon a `Spacer` a component builds took no space in a column: the second box stood at 10 where
  34 is due (#736). A line asked the component whether it was a Spacer or a Flexible, and a component
  answers neither.
- An `AdaptiveNode` a component builds was not placed by its flex or grid parent: an arm's `AlignSelf`
  and `GridSpan` were dropped, on the web and on Photon, where the same arm written directly is placed
  (#738). A `Stack` already saw through a component to its arms; a line and a grid did not.
- On the web a `VisualNodeComponent` given its own theme built a stateless component in the AMBIENT one
  in the browser, so a checkbox the server drew green hydrated in the page's colours (#701).

## What Changes

- On Photon `LaidOutChildren` answers for what a component built when that is a node only its parent
  can place (a Spacer, a Flexible, an AdaptiveNode), read from the element its position already built,
  so nothing builds twice and the instance that built stays. Any other child keeps the component's own
  node. It now holds the parent's element, so `At(index)` takes no parent.
- On the web a container's rule waits for what a component builds, in both producers: an AdaptiveNode
  out of a component has its arms placed by that rule, and anything but another component ends the
  wait. The component still builds once, through the ordinary door.
- A bridge's theme and type scale are the ambient ones for the length of its render, and are handed
  back after it. The twin now honours the type scale the bridge was given, as the server does.

For a developer: wrapping a `Spacer`, a `Flexible` or an `AdaptiveNode` in a component no longer
changes where it stands, on either target, and a bridge's subtree hydrates in the colours the server
drew. No public surface moves.

## Capabilities

### Modified Capabilities

- `adaptive-layout`: an arm a component builds is laid out by the parent the component stands in.
- `hydration-contract`: a bridge's subtree reads one theme.

## Impact

- `src/eQuantic.UI.Native.Framework/Layout/LaidOutChildren.cs` and its callers.
- `src/eQuantic.UI.Web/WebLoweringVisitor.cs`, `WebLoweringVisitor.Containers.cs`, and in the runtime
  `shared/lowering.ts`, `shared/visual-node-component.ts`, `shared/photon-context.ts`.
