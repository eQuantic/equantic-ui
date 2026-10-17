# Design

## Context

Photon draws a frame by laying the tree out from its root, and `Build` runs inside that walk:
`MeasureVisitor.MeasureComponent` asks `ComponentInstanceStore.Reconcile(path, component)` for the
instance to use, then expands it through `ComponentBoundary.ExpandContained`. The store keys a
stateful instance by `"{path}#{type.FullName}#{key}"`, where the path is the layout's child path, and
`EndPass` unmounts what the pass did not visit before it mounts what entered. Every frame the host
draws rebuilds every component, once per measure of it: a stack or a reflowing box measures twice,
and intrinsic sizing builds a third time without reconciling, so a stateful child is measured there
with a fresh instance's state. The root is the same instance every frame, so the store's "new" branch
fires `InstanceRetained` for it every frame and the host subscribes again (#935).

The rest of the per-position state (scroll, presence, drag, in-view, transitions), focus, hover and
the hit regions are keyed by the same paths, and stay so in E1: E2 moves them onto elements.

Measured before any code, on main `d702ca101`, with the stateful scene this change adds to
`PerfHarnessTests` (a dashboard of 24 stateful cards and a loop strip): 98,480 bytes per steady frame
with recycled frames, against 64.2 KB for the dense scene, which has no component at all.

## Goals / Non-Goals

**Goals:**

- One element per node of a Photon tree, kept across frames, and a stateful instance kept by its
  element: the identity the store spelled as a string, held as an object.
- A build phase before layout, owned by a `BuildOwner`, that builds only what is dirty, ancestors
  first; layout that builds nothing.
- The lifecycle order the store guaranteed (what leaves unmounts before what arrives mounts), now by
  the owner, and #935 closed because nothing subscribes per instance.

**Non-Goals:**

- The browser and the server (E3, E4). Their instance handling is untouched.
- The node state stores, focus, hover and hit regions moving off paths (E2). Paths are computed
  exactly as today, beside the elements.
- `InheritedComponent`, `DependOn`, a positional `ComponentContext` (E5).
- Removing `ComponentContext`'s public constructor: plan 3.3 takes it after the testing harness
  (#294), and about 25 tests and three realizers still make one.

## Decisions

### Flutter's three trees, the middle one added

*How does Flutter solve it?* `Widget` is configuration, `Element` is the persistent position (and the
`BuildContext`), `RenderObject` is layout and paint. We had the first (`VisualNode`) and the third
(`LayoutNode`); the middle one was spread across the store and the paths. `docs/FLUTTER-PARITY.md`'s
`Element` row was a GAP and becomes PARTIAL: the tree exists on Photon, the context is not yet the
element (E5), and the other targets keep their own until E3 and E4. The `setState` row now probes the
owner rather than `StateInvalidated`.

### Two kinds now, the third with its component

`ComponentElement` builds; `NodeElement` holds a vocabulary node's children by slot. The plan lists
`InheritedElement` in E1; it arrives in E5 with `InheritedComponent`, because a kind with no node to
hold would be code no test can reach.

### The identity rule is the store's, held as an object

`canUpdate` is Flutter's: the same runtime type and the same `Key`. One stateful instance a parent
places at two positions at once shows at both: a `SetState` builds both, and it stays mounted until
the last of them leaves. Flutter refuses a `State` at two positions; here the instance is both the
configuration and the state, and the store rendered it twice, so the element keeps that. Which old
element a new child is compared with follows the paths exactly, so no retention changes: a
single-child slot compares its one occupant; the children of a `Row`, a `Column`, a `Stack`, a
`Grid` and a `Navigable` (the nodes whose paths spell a key as `[key]`) are matched by key when they
have one and by index when they do not. Duplicate keys, which the store resolved to one shared
instance, are matched in order, first to first and second to second, giving the second child an
element of its own.

The string model was wrong in three places that the objects correct: the grid's hug-column probe
measured every candidate at one shared path (`…/probe`), so stateful candidates of one type shared an
instance; an overlay's components were keyed by the overlay's index in the frame's queue, so closing
one layer handed the next one its state; and intrinsic sizing measured fresh instances.

### Build is a phase, ancestors first

`BuildOwner` holds the dirty list and builds it as `BuildOwner.buildScope` does: sorted by depth,
skipping what left the tree and what already built in this frame, so a position builds once a frame.
A position marked while the phase runs (its own build marking it, or a hook) builds in the next
frame, which keeps a component that always marks itself from holding a frame open. A position under a
deferred slot (below) is not built from the list: it waits for the overlay pass to give the slot its
configuration, and builds then, or leaves with the slot when the layer is not shown. A component element's update rebuilds it at once, as Flutter's
`ComponentElement.update` does, so a parent's build reaches every child whose configuration it
replaced; a child whose configuration is the same instance as before is left alone unless it is dirty.

The children of a node are enumerated by one visitor over the vocabulary, `ChildSlots`, so a node kind
added to `IVisualNodeVisitor` cannot be forgotten by the engine. It resolves an `AdaptiveNode` to the
arm of the frame's window size class, as layout does, and reports which slots are deferred (below).

### Layout carries the element beside the path

The measure walk's state was `(constraints, path)` and becomes `(constraints, position)`, where a
position is the path and the element together, so every site that formed a child's path now forms the
child's position and the two cannot drift. A component is measured as a wrapper of the subtree its
element built; intrinsic sizing reads the same subtree. Measuring a node with an element that does not
hold it throws, naming both: it is the engine's one invariant, and a layout that silently measured a
stale subtree would show it as a frame that is merely wrong.

A one-shot `LayoutEngine.Layout` with no root element (the tests, the tools) builds its own tree with
a structural owner first, so there is one measure path.

### A layer builds when the realizer shows it

The realizer decides whether a layer is shown: an `Overlay` closed with motion is realized nowhere,
and an `Anchored` panel opens on its `Open` or on the hover the host reports at paint. So those two
slots, an overlay's child and an anchored panel, are DEFERRED: the build phase leaves them, and the
overlay pass reconciles them when it realizes the layer, just before laying it out. At the end of the
frame the owner sweeps every deferred slot that was not reconciled in it, which unmounts a closed
layer's components as the store did. Building them eagerly would have kept a closed select's options
mounted and built on every rebuild.

The anchored layer is synthetic: a scrim and a `Positioned` around the panel, which the paint walk
makes once the anchor is laid out, since the panel is placed from the anchor's bounds. So the
anchored node's deferred slot holds the whole LAYER. The walk makes a new one every frame and its
elements are updated in place; the panel inside is the same node frame after frame unless the
anchored node's parent rebuilt, so it builds only when it changed. Measuring the panel against an
element handed to the layer's layout from outside was the first design, and it needed a second way
for a node to find its element, by reference, which a node shared by two positions answers wrongly.

### The owner orders the hooks

Unmounts are queued as elements leave, children before parents, and mounts as stateful elements
enter, parents before children; `EndFrame` runs every unmount and then every mount, each idempotent
on the instance. A structural owner (a one-shot layout) queues neither and sets no back-reference, so
a component laid out once by a test is not held by a tree nobody draws.

### SetState reaches its element; the host listens once

A mounted `StatefulComponent` holds its element. `SetState` keeps its dispatcher marshalling and marks
that element dirty instead of raising `StateInvalidated`, which goes. The host subscribes to its
owner's request for a frame once, in its constructor, which closes #935 by construction: there is no
per-instance subscription left to repeat.

### A request made after the build survives the frame's end

The host ended every frame by writing its request for the next one as "motion is running", which
wrote `false` over a request made after the build: a `SetState` from a scroll view's
`OnViewportChanged` or `OnScrolled` (fired while the frame paints), from an `OnMount` at the frame's
end, or from a build itself. The store hid it, since every frame rebuilt everything anyway and the
next input drew the change; with only the marked positions building, the marked one would have waited
for the next input, and a second `SetState` would not ask again. So the end of the frame keeps the
request while the owner holds a marked position. It is not hypothetical: a `ListView`'s first frame
reports the true viewport and asks for its window, which waited for the next input on main.

### Dirty-only, with the host's input rebuilding everything

A frame the host draws only to move something (a glide, a transition, a presence, a loop motion, a
frame ticker's subscriber) runs the build phase over the dirty list alone, which is empty unless a
`SetState` happened. Every other request for a frame (input, `Invalidate`, a resize, a hot reload, a
dispatcher wake) rebuilds every component, as every frame does today. The reason is measured rather
than cautious: the host routes keys and pointers into models the tree reads without a `SetState`
(the code editor's and the spreadsheet's controllers, a text field's draft), and it addresses them by
path until E2. Narrowing that rule is E2's, once the regions carry elements; until then the gain is
every animation frame, which is the hot path.

### The boundary stays at build

The build phase builds each component through `BuildContained`, so a throw is contained exactly as
before. The nesting bound becomes structural: a component element knows how many component elements
are above it, and the sixty-fifth is contained with the same sentence `ExpandContained` uses, instead
of a thread-static counter that only a recursive walk could keep.

`InFlow` is an ambient armed while a component builds. A dirty component builds alone, outside its
parent's walk, so its element remembers whether an `InFlow` is above it and the build arms the ambient
from that, writing it only when it differs.

### Host-only until the browser needs it

The plan writes the engine once and transpiles it into the runtime. E1 marks it `[ServerOnly]`,
because no browser code drives it until E3, and a type routed to the runtime with no twin dies at
hydration. What eqc refuses in it today is measured (below) so E3 starts from the list.

## Risks / Trade-offs

- [A frame after input costs what it costs today] → by design (above); E2 narrows it.
- [The invariant throws in a shipped app] → it fires only on a path the engine got wrong, which the
  suites and goldens exercise on every node kind; a silently stale frame is the alternative.
- [Two identities coexist until E2: elements for components, paths for node state] → paths are
  formed exactly as before, so nothing keyed by them moves.
- [An element per node] → allocated once per position and updated in place; the allocation per frame
  is measured below.

## Measurements

`PerfHarnessTests`, steady frames with recycled frames (the shells' configuration):

| | main `d702ca101` | elements, every frame rebuilt | elements, dirty only |
|---|---|---|---|
| dense scene (24 cards, 24 `Button` components) | 64.2 KB | 64.2 KB | 38.3 KB |
| stateful scene (a stateful page, 24 stateful cards) | 98,480 B | 94,110 B | 28,742 B |
| dense scene, frames not recycled | 114.8 KB | 116.7 KB | 90.9 KB |
| an overlay layer | 498 B | 498 B | 498 B |

The middle column is the engine alone: every component builds once a frame instead of once per
measure, and no identity string is formed. The last is what a frame drawn for motion costs once it
builds nothing. The budgets are tightened to it (pooled 72 KB to 42 KB, unpooled 152 KB to 100 KB) and
the stateful scene gets one of its own (32 KB).

What eqc refuses in the engine, measured by running eqc over its sources moved into an app's
namespace, with the Primitives internals it reaches stood in by public calls: the forty-one
`ChildSlots.Visit` overloads, which lower to one `visit()` (EQ1007, a JavaScript class has one member
per name); `GetType()` in `Element.CanUpdate`, Flutter's `canUpdate` (EQ2004); and `GetType().Name` in
`NodeElement.ShowDeferred`'s message. Nothing else: the lists, the dictionary, the static comparison,
the tuple swap, the patterns and the event all transpile. E3 starts from these three.
