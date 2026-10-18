# Design

## Context

E1 (#950) gave every node of a Photon tree an element that lives as long as its position, and moved
the stateful instances onto them. The rest of what a node remembers between frames stayed in five
stores keyed by the node's layout path, lent to the layout and paint walks every frame:

| store | what it keeps, by path | forgets a path |
|---|---|---|
| `ScrollStore` | a scroll view's offset, and its target while it glides | never |
| `DragStore` | a drag's offset, and its glide back | never |
| `InViewStore` | what an `InView` last reported | never |
| `TransitionStore` | the tracks of a transition, one per channel, and a shadow list's length | never |
| `PresenceStore` | when a `Presence` was first seen, its last paint, the exits in flight | at the end of a frame it was not measured in |

A path renumbers when an earlier sibling appears, and it does not change when the node at it does: a
key on a single-child slot, a component replaced by one of another type, a page swapped by a
`SetState`. The four stores that never forget hand what the old node left to the next one. The
presence store forgets, so a path whose node changed keeps its entrance and loses its exit.

The host writes two of them from input, by the path a region carried: the pan and the wheel move a
`ScrollStore` offset, the drag a `DragStore` one.

Measured before any code, on main `f2369d2fa`, with recycled frames (the shells' configuration): the
dense scene 38.3 KB a frame, the stateful dashboard 28,742 bytes, a layer 498 bytes, and the dense
scene unpooled 90.9 KB. None of those scenes keeps node state, so this change adds one: the dense
scene in a scroll view with every card gliding (`GlidingScene`), measured on main at 73,669 bytes a
frame, under Measurements.

## Goals / Non-Goals

**Goals:**

- What a node remembers between frames belongs to its element and leaves with it, so the next node
  at its place starts with its own.
- A presence that leaves still fades out from its last paint, from the frame it left in, as the store
  did: the presence tests that drive a host and the goldens pass unchanged.
- The host's gestures that write a node's state (the pan, the wheel, the drag) address the element.

**Non-Goals:**

- Focus, hover, the press, the text being edited, the caret, the live regions and the regions that
  carry them (#976). They keep their paths for one more slice.
- The accessibility ids and the path cache (#976).
- Narrowing the rule that input rebuilds every component (#976, once every region carries an
  element).

## Decisions

### Where Flutter keeps it

*How does Flutter solve it?* A node's own state lives in an object bound to its element: a
`Scrollable`'s offset in its `ScrollableState`'s `ScrollPosition`, an implicit animation's progress in
its `ImplicitlyAnimatedWidgetState`'s controllers, a draggable's drag in its gesture recognizer's
state. When the element unmounts, the `State` is disposed with it, and a new element at the same place
gets a new `State`. What runs over time registers a `Ticker` with the scheduler, which keeps asking
for frames while any ticker is active. `docs/FLUTTER-PARITY.md`'s `Element` row stays PARTIAL (the
context is not the element until E5), and the change is the half of it that says "state belongs to a
position".

Here the vocabulary's nodes are not components, so they have no `State`: a scroll view is a node the
realizer gives meaning to. The element holds a slot for what the realizer keeps for its node, and the
host keeps the tickers.

### A slot on a node's element, released with it

`NodeElement.State` is the slot: one object, set and read by the realizer, never read by the engine.
When the position leaves the tree the engine clears it and disposes it when it is an `IDisposable`,
after its children's, in the order Flutter disposes states. It is an `object` because the engine is
written once for every target and transpiled in E3: each realizer types its own state, and the
browser's will not be Photon's. It is on `NodeElement` only, because a component's position already
keeps its state, the instance.

One slot is enough, because the kinds of state are disjoint by node type: a `ScrollView` keeps a
scroll, a `Draggable` or a `DragDismiss` a drag, an `InView` its last report, a `Presence` its
entrance and its last paint, and a styled box (a `Box`, a `Pinned` header) or a weighted child (a
`Flexible`, a `Spacer`) its transition tracks. The accessor that reads and makes them
(`NodeStates`) refuses a slot that already holds another kind, naming both, as the measure refuses a
node handed the wrong element: two kinds at one position is the engine gone wrong, and the frame
would otherwise be merely wrong.

### What stays the host's: four clocks

What was per path moves to the element; what is per host stays in an object the host keeps and lends
to the realizer, as it lent the stores. Each is renamed for what is left of it, because a
`ScrollStore` that stores no offset is a name that lies:

- `ScrollClock`: whether a wheel glides (`Smooth`), the scroll views gliding toward a target, and
  their advance before each frame. The host moves a view through it, by the view's element; the
  layout and the paint read the offset off the element and need no clock.
- `DragClock`: the drag's follow, its glide back and its drop, by element, and whether a glide back is
  still running this frame.
- `TransitionClock`: a position's tracks resolved against the frame's clock, and whether any is still
  moving.
- `PresenceClock`: whether entrances run at all (a frame realized without one draws every presence
  settled, which the goldens rely on), the entrance progress, the snapshot of each presence's paint,
  and the exits.

`InViewStore` goes with nothing left of it. An `InView` reports in a tree that is drawn again, which
is a frame realized with a live owner: the rule E1 gave the lifecycle hooks, for the same reason, a
callback that sets state for a tree nobody draws again. The `BuildOwner.Live` it reads becomes
public.

### A presence leaves through its state

The plan says the exits move to the owner. They are Photon draw commands and the owner is written for
every target, so they move to the host's presence clock instead, which the owner reaches through the
slot: a presence's state is disposed when its element leaves, and hands its last paint to the clock it
was made by, stamped with the frame's time. The clock replays the exits as the store did, paint only
and above everything.

The store noticed a departure at the end of a frame, after the overlay pass, so a layer that closed in
a frame showed its exit in that frame. An element leaves either during the build (its parent stopped
building it) or at the frame's end (a layer not shown, which the owner sweeps). So the realizer now
ends the frame on the owner before it replays the exits: the sweep and the lifecycle flush come first,
and an exit is drawn from the frame its presence left in, wherever it left.

A presence that takes the place of one that left enters while the old one fades: the store, keyed by
path, gave the new one the old entrance and gave the old one no exit. This is the cross-fade the web
gets from a new DOM element, so the two targets now agree.

### Tracks keyed by what they glide

A track was found by a string formed every frame: the node's path and a channel, `r/0/2:opacity`, and
for a colour four more, `…:bg.r`. A transitioned box formed about twenty-five strings a frame, settled
or not. The tracks now live in the box's state, keyed by a `TrackKey`: what it glides (a
`GlideChannel`) and, for a member of a list, its index. A colour is four tracks under one key, as it
was four strings, so each channel retargets on its own as before. Nothing is formed to find a track.

### A scroll view's range on its laid-out node

The measure recorded each scroll view's path and range in a dictionary made every frame
(`ScrollMeta`), keyed by the node, which the paint walk read to register the region. The range is now
a field of the laid-out node (`LayoutNode.ScrollRange`), which is exactly "the range of the measure
the tree keeps", without a dictionary per frame. The region carries the element; it keeps its path
too, for one slice, because the host still finds the scroll view that contains a focus stop by path
(`IsAncestorPath`), until #976 gives the stops elements and the containment walks the parents.

### Gestures hold the element

The host's pan and drag hold the element of the region they armed on. A gesture whose node left
mid-flight writes to a state nobody draws, where the store wrote to whatever node took the path. The
host's `ScrollOffsetOf` takes the element, which a test reads off the region.

### The grid's probe reads the child's own state

A grid measures a hug column's candidates at a probe path, `…/probe`, with each child's own element
since E1. The stores kept the probe's state under the probe path, a second copy of every state below
the probed child that nothing drew and nothing forgot. Keyed by element, a probe reads and writes the
child's own state, and resolving a track twice at one frame's time answers the same.

## Risks / Trade-offs

- [Two kinds of state at one position] → impossible by construction (the kinds are disjoint by node
  type), and refused loudly if the construction ever changes.
- [A presence replaced at its place now cross-fades] → a visible change on a native shell, toward
  what the web does; no golden draws it, and a host test pins it.
- [The scroll region carries a path and an element for one slice] → the path is read only by the
  containment questions #976 rewrites; nothing writes state by it.
- [An element per stateful node keeps a small object for as long as it lives] → allocated once per
  position, where the stores grew a dictionary entry per path forever.

## Measurements

`PerfHarnessTests`, steady frames with recycled frames unless said otherwise:

| | main `f2369d2fa` | this change |
|---|---|---|
| dense scene | 38.3 KB | 38.1 KB |
| stateful dashboard | 28,742 B | 28,534 B |
| gliding scene (new) | 73,669 B | 39,133 B |
| an overlay layer | 498 B | 482 B |
| dense scene, frames not recycled | 90.9 KB | 88.7 KB |

The gliding scene loses what finding its tracks cost: about twenty-five strings a card a frame,
formed to look a track up and dropped. The other scenes keep no node state and lose the per-frame
`ScrollMeta` dictionary and the list `ActiveExits` made when nothing was exiting. The gliding scene
gets a ceiling of its own, 43 KB, that number and about a tenth.

Whether the layout node pool still earns its place (plan, section 5): yes. The dense scene allocates
88.7 KB a frame without it and 38.1 KB with it, so it saves about 50 KB a frame. Nodes are still
made per frame, since a `LayoutNode` is a frame's measure and not a position, so the pool is what
keeps them from being garbage.
