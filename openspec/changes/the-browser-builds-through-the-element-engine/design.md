# Design

## Context

Measured on the branch this slice starts from (#1405's head, bc42a5648):

- `ComponentInstanceStore` (`shared/instance-store.ts`) keeps a page's stateful instances keyed
  `` `${path}#${componentIdentity}#${key}` ``. A pass opens around a page's render (`enterPass`), the
  outermost owner wins, and an island or a bridge rendered inside the page joins it. `endPass` drops what
  was not visited, runs every `onUnmount`, then every `onMount`.
- Every `lowerVisualNode` inside a pass takes the next root, `r0`, `r1`…: one per web component whose
  render lowers a vocabulary tree (a page root, a bridge, a Core component composing shared ones).
- The lowering's `'component'` arm reconciles against the store, adopts server state under the next
  `Type#ordinal`, and calls `build` inside a boundary. `resolveStackChild` does the same for a stack's
  component child, up to eight hops.
- A nested `SetState` runs the instance's `_invalidationHook`, the page's invalidator, and the page's
  `render()` builds the page again: every component on it builds.
- The C# web realizer lowers a frame of a live tree through `ElementFrame`: a dictionary from a node to
  the position it stands at, asked for the instance a component's position keeps and what it built,
  for the arm an adaptive node's position holds, and to show a layer in a holder's deferred slot.

## Goals / Non-Goals

**Goals:** the browser builds through the transpiled engine; a nested `SetState` builds its own element
and no other; hydration builds the element tree before it walks the server's DOM; the runtime's suites,
the hydration contract and the e2e suite pass; the served runtime stays inside its budget.

**Non-Goals:** the path attributes and module maps (E3b-2, #1454); lowering only the dirty subtree and
patching its DOM range (E3b-3, #1455, outside 1.0); the shell that survives a navigation (E3c, #1129);
inherited components and the positional context (E5b, #1395); hot reload's capture of every element
(section 3.8), which stays the page root's.

## Decisions

### An owner per root, kept by the pass

The pass keeps its shape and its rule (the outermost wins), and what it holds changes: in place of a
store, the page's list of owners, one per root slot. `lowerVisualNode` inside a pass takes the next slot
(`r{n}` stays its path prefix) and builds that slot's owner. At the end of the pass, a slot this render
did not reach is taken down with `UnmountAll`, as the store dropped what a pass did not visit.

One owner for the whole page would be Flutter's shape, and the browser cannot have it yet: the roots are
found while the lowering walks a Core tree, which is `HtmlElement`s the engine does not hold (as in C#),
so they are not known before the walk. E3c's shell moves the owner above the page, and that is where one
owner per app belongs.

*Alternative rejected:* a synthetic root node holding the slots, built once the walk has found them. It
builds after the lowering that needed what it builds.

### What leaves unmounts before what arrives mounts, per root

Each owner's `EndFrame` runs its own `onUnmount`s before its `onMount`s. Across the roots of one page the
owners end in slot order, so a component leaving root `r1` unmounts after one arriving in `r0` mounts. The
store ordered all of a page's departures before any arrival. The pair that rule protects (two components
handing one exclusive resource across a swap) is a swap at one position, which is one root. If a spec
pins the order across roots, the engine gains the split of `EndFrame` into its two halves rather than
the browser reordering them itself.

### A render the owner asked for lowers the tree the page last built

The page caches what its last `build` returned. When the owner raises `BuildRequested` (a nested element
was marked), the page's next render lowers that cached tree again: every root hands its owner the same
node it handed last time, `Build` finds the configuration unchanged and builds only the dirty elements.
Building the page again would hand every position a fresh configuration, and the engine rebuilds a
component whose configuration changed: the slice would change nothing.

Any other render builds the page, as today, and asks every owner to rebuild everything (`rebuildAll`):
the page's own `SetState`, a navigation, a hydration, a change of theme, density or culture, a hot
reload. A render requested by both a mark and something else is the second kind.

### The frame is the lowering's, not an ambient

A frame indexes a built root by node identity (`Map<object, Element>`), as `ElementFrame` does, and
travels in the `LoweringContext`, as the C# frame is a field of the lowering visitor. The component arm
asks it for the kept instance and what that instance built, and lowers the built node at `path + '/0'`.
A component the frame does not hold is an error, as in C#: lowering a fresh instance instead would draw
its defaults in silence. The adaptive node's arms and the deferred layers (an overlay's child, an
anchored panel) ask the frame exactly where the C# visitor does (`HoldsArm`, `Show`). The frame is
hand-written beside the lowering, like the lowering itself: the engine is written once and transpiled,
and the index over it is the realizer's.

A lowering with no pass (a test, a direct embedding) builds through a structural owner and discards it,
which is what the server does for a page.

### The owner's build callback holds the boundary and the server's state

The callback the runtime hands the owner builds a component inside today's boundary and, before an
instance's first build, adopts what the server loaded for it under the next `Type#ordinal`. The engine
builds a frame depth first, pre-order, which is the order the lowering walked, so the two sides count
the same components in the same order. The ordinals restart at every walk that builds the page, as they
do now; a render the owner asked for adopts nothing, since no instance in it is new.

### The invalidation hook goes

A stateful instance's `SetState` marks the elements it is mounted at (`mountedAt`, E3a-3), and their
owner asks for the render. `_invalidationHook`, which pointed every nested instance at its page, is
removed with the store.

## Risks / Trade-offs

- **The order across roots** is narrower than the store's (above). Measured by the lifecycle specs; the
  engine is changed if one pins it.
- **A page whose build reads something outside its state** (a module global, the time) and relies on a
  nested `SetState` to make it build again would no longer build. The runtime's own components are read
  for it by the suites; an app's would see what Flutter's sees, which is the model this plan adopts.
- **Two stacked pull requests:** this slice is stacked on #1405 until it merges.
