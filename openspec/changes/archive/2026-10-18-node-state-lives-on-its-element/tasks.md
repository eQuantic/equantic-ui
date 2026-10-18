# Tasks

## 1. Measured before

- [x] 1.1 `PerfHarnessTests` gains the gliding scene (the dense scene in a scroll view, every card
  gliding), measured on main's code with the other scenes, the numbers in design.md

## 2. The slot

- [x] 2.1 `NodeElement.State`, cleared when the position leaves and disposed when it is an
  `IDisposable`, children first; `BuildOwner.Live` public
- [x] 2.2 Engine tests: a state set on a position survives a rebuild that keeps it, and is disposed
  once when a key change, a type change, a parent's removal, an unshown layer or `UnmountAll` takes
  the position out

## 3. Photon's states and clocks

- [x] 3.1 `NodeStates`, the accessor that reads and makes a position's state and refuses a second kind
- [x] 3.2 `ScrollState` and `ScrollClock` (smoothing, the gliding views, the advance, a released view
  dropped from the glide), replacing `ScrollStore`
- [x] 3.3 `DragState` and `DragClock`, replacing `DragStore`
- [x] 3.4 `TransitionState`, `TrackKey`, `GlideChannel` and `TransitionClock`, replacing
  `TransitionStore`, with no string formed to find a track
- [x] 3.5 `PresenceState` and `PresenceClock`: the entrance on the state, the exit handed to the clock
  when the state is released, stamped with the frame's time; replacing `PresenceStore`
- [x] 3.6 The measure reads a scroll offset, a drag, an entrance and a track off the position's
  element; `LayoutNode.ScrollRange` replaces `ScrollMeta`; `PresencePath` and `DragPath` go
- [x] 3.7 Check: the store tests rewritten against the clocks and the elements, and the native suite

## 4. The paint walk, the realizer and the host

- [x] 4.1 The paint walk resolves opacity, transform, shadow and colour tracks, the presence snapshot
  and the in-view report against the node's element; `InViewStore` goes, and an `InView` reports only
  in a frame realized with a live owner
- [x] 4.2 `ScrollRegion` carries the element beside its path; `DragRegion` carries the element in
  place of its path
- [x] 4.3 The realizer takes the clocks, ends the frame on the owner before it replays the exits
- [x] 4.4 `PhotonHost` keeps the four clocks, its pan and drag hold elements, and `ScrollOffsetOf`
  takes the element
- [x] 4.5 Host tests, each failing on main: a page that replaces another starts its scroll view at its
  own offset, and its `InView` reports its first sight; a key change gives a scroll view its own
  offset; a draggable replaced mid-glide rests; a box that replaces another mounts at its width; a
  presence replaced at its place exits while the new one enters. And one that passes on both: a scroll
  view that keeps its place keeps its offset
- [x] 4.6 Check: the native suite, every golden unchanged, and the presence tests that drive a host
  unchanged; `PhotonDesktop`'s gallery pictures through the macOS shell's windowless frame, pixel for
  pixel against main

## 5. Measured after

- [x] 5.1 The scenes after the change beside the baseline in design.md; the gliding scene's ceiling;
  whether the layout node pool still earns its place (pooled against unpooled)

## 6. Documentation

- [x] 6.1 `docs/ELEMENT-TREE-PLAN.md`: E2's first half landed, what it measured, and its deviations
  (the exits on the host's clock, the scroll region's path kept for #976)
- [x] 6.2 The docs and code comments that describe the stores, `docs/FLUTTER-PARITY.md` where a row
  names them
- [x] 6.3 The wiki pages that describe a node's state on Photon, in their five languages, on the wiki
  branch named like this one
- [x] 6.4 `./scripts/public-api.sh update`, the diff read: the stores and the moved members as
  `*REMOVED*` lines, the clocks and the slot as additions
- [x] 6.5 One ledger entry under `docs/ledger/`, citing #975
- [x] 6.6 Check: `./scripts/check-openspec.sh`, `DocsIndexTests`, the wiki guards
