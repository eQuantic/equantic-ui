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
its defaults in silence. The deferred layers (an overlay's child, an anchored panel) ask the frame
exactly where the C# visitor does (`Show`). The frame is
hand-written beside the lowering, like the lowering itself: the engine is written once and transpiled,
and the index over it is the realizer's.

A lowering with no pass (a test, a direct embedding) builds through a structural owner and discards it,
which is what the server does for a page.

### Every arm of an adaptive node is a position on the web (#1477)

The web draws every arm an adaptive node declares, each behind a media gate, and a resize opens another
gate without a render. An owner that held only the arm a window size class resolved left the arm on
screen, at some widths, as one no element held: its stateful components started again on every build,
and a resize needed a build to put the shown arm under an element. So the web's owners hold every arm
(`BuildOwner(…, holdsEveryArm: true)`), compact at slot 0, medium at 1 and expanded at 2, and nothing on
the web resolves an arm: `MarkupHost.Build` takes no size class, the browser's owners are handed a
constant nobody reads, and the lowering of an arm with no frame, in C# and in TypeScript, goes with the
listener that rebuilt the page when the window crossed a threshold. Photon draws one arm and keeps
resolving it.

### Server data is keyed by a component's position (#1477)

The server named a component's data `Type#ordinal` as its lowering met the component, which was the
browser's order while the browser's lowering built. Once the browser builds through the engine, the data
has to reach an instance before it builds, so the browser could only count in build order, and the two
orders part at the arms a position does not hold (the server held `Expanded`, the browser the window's,
and both built the others during the lowering) and at a deferred layer, built when the lowering shows
it. Two components of one type, one in such an arm or layer and one after it, swapped their data. And a
render the owner asked for named nothing, so a stateless component handed anew at a kept position got
none.

Lowering order exists only after the build, and adopting in rounds, as the server loads, would lower the
whole page once per data component on hydration. A key read off the position needs neither: the tree of
the render, the type, and the slots from the tree's root down (`Element.SlotsBelow`, with `Element.Slot`
recorded where a parent places a child, Flutter's slot). Both sides read it off the same engine. The
browser's build callback asks the owner which position is building (`BuildOwner.Building`) and adopts
under that key on every build of a fresh instance; a kept stateful instance took its data when it
mounted. The server names the kept instance by the frame's key as it lowers; an escape-hatch page's
bridge is drawn once through a structural host of its own (`MarkupHost.DrawOnce`), which puts back what
an earlier round loaded before a fresh instance builds (`ComponentExpansionScope.PutBack`). A page's
own component keeps `Type#0`, and the server measures a page's keys below the page
(`MarkupHost.ForPage`), since the browser builds a page's component by itself.

The callback also keeps today's boundary: a throw costs the component's own subtree.

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
