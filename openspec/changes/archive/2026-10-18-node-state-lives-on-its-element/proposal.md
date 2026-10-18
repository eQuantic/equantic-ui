# Proposal

Closes #975, the first half of #973 (E2), which is a sub-issue of #937 (the element tree, E1 to E5)
under the 1.0 epic #898. The design it follows is `docs/ELEMENT-TREE-PLAN.md`, sections 3.1 and 3.7.
The second half, #976, points the rest of the host at elements and removes the paths.

## Why

Photon keeps what a node remembers between frames in five stores keyed by the node's layout path:
`ScrollStore` (a scroll view's offset), `DragStore` (a drag in progress or gliding back),
`InViewStore` (what an `InView` last reported), `PresenceStore` (a `Presence`'s entrance and its last
paint) and `TransitionStore` (a transition's tracks). Four of them never forget a path, so a node that
later takes the same path inherits what the last one left there. A page that replaces another starts
its list scrolled to where the old page's list was, a box mounts by gliding from the old box's size,
and an `InView` at the place another one stood never reports that it is visible. E1 gave every node
an element, the position Flutter keys state by, so the state can now live where it belongs.

## What Changes

- The state a node keeps across frames lives on its element and leaves the tree with it. A node that
  takes a position another node held starts with its own state: a scroll view at its own `Offset`, a
  draggable at rest, an `InView` reporting its first sight, a transition mounting at its value, a
  `Presence` entering.
- A `Presence` whose element leaves hands its last paint to the host's presence clock, which replays
  it as an exit, paint only, from the frame it left in, as the store did by path.
- What is left of each store is the host's part of it, renamed for what it does: `ScrollClock` (the
  smoothing policy and the views gliding toward a wheel's or a fling's target), `DragClock` and
  `TransitionClock` (whether a glide is still running), `PresenceClock` (whether entrances run, and
  the exits). `InViewStore` goes: an `InView` reports in a tree that is drawn again, a frame realized
  with a live owner, which is when a host draws it.
- The scroll and drag regions carry the element of their node, and the host's pan and drag hold it.
  Focus, hover, the press and the other regions keep their paths until #976.
- What a developer sees: nothing they write changes. On a native shell, the state of one node no
  longer passes to the next node drawn at its place, and a page that replaces another with a
  `Presence` at the same place now shows the old one leaving while the new one enters, as the web does.
- **BREAKING** (host plumbing in the experimental native assemblies, none of it called in
  `samples/`): `TransitionStore`, `ScrollStore`, `PresenceStore`, `DragStore` and `InViewStore` are
  gone, replaced by the four clocks; `PhotonRealizer.Realize` loses `scrollOffsets`, `scrollOffset`
  and `inViewStore`, and its `transitions`, `presences` and `drags` take the clocks;
  `ScrollRegion` gains `Element`; `DragRegion.Path` becomes `DragRegion.Element`;
  `PhotonHost.ScrollOffsetOf` takes the element; `LayoutNode.PresencePath`, `LayoutNode.DragPath`,
  `LayoutContext.ScrollOffsets` and `LayoutContext.ScrollMeta` are gone. The migration line: a host
  keeps the clocks where it kept the stores and passes its owner, and a test that asked the host for
  a scroll offset by a region's path asks by the region's element.

## Capabilities

### New Capabilities

### Modified Capabilities
- `element-tree`: a position keeps its node's state as it keeps a component's instance, and a
  `Presence` that leaves fades out from the frame it left in.

## Impact

- `eQuantic.UI.Primitives`: `NodeElement.State`, the slot a target keeps a node's state in, released
  when the position leaves; `BuildOwner.Live` becomes public. Additions only.
- `eQuantic.UI.Native.Framework`: the five stores replaced by per-position states and the four
  clocks; the measure reads a node's state from its element; `LayoutNode` carries a scroll view's
  range in place of the per-frame `ScrollMeta` dictionary.
- `eQuantic.UI.Native.Components`: the paint walk resolves transitions, presences and in-view reports
  against elements; the realizer replays a presence's exit after the frame's end, so a layer that
  closes shows its exit in the same frame; `PhotonHost` holds elements for its pan and drag.
- Not reached: eqc, the runtime, the web realizer, the server, the SDKs, the templates, the shells
  (none calls what moved), the developer surface (`developer-surface.baseline.txt`).
