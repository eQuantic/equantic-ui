# Design

## Context

Measured on the slice's base (E3b-1's branch at dcf2a80af):

- `lowering.ts` threads a path through every `lower*` call. It writes it into seven attributes and
  three ids, and keys the code editor's revealed versions (`revealedVersions`, a module `Map` nobody
  clears) by it.
- The scroll, table, in-view and canvas after-passes collect a declaration per pass in a module `Map`
  keyed by the path. Each commit finds the DOM elements by the attribute, and keeps what it must
  remember on the DOM element as expandos (`__eqScrollAdopted`, `__eqRequested`, `__eqInViewVisible`,
  `__eqTableReported`…). The reconciler reuses a DOM element for whatever node is lowered at its place,
  so the expandos pass to that node.
- `Element.Id` comes from `BuildOwner.NextId()`, one counter per owner. E3b-1 gives a page one owner per
  root slot.

## Decisions

### State on the position, as Photon keeps it

`node-states.ts` is the twin of Photon's `NodeStates`: `nodeStateFor(position, Kind)` makes or answers
the position's one state, and `findNodeState` reads it. Both refuse a position that keeps another kind.
Each after-pass owns its kind (`ScrollViewportState`, `TableViewportState`, `InViewState`,
`CanvasState`), and the code surface keeps a `CodeSurfaceState`. The lowering asks the frame for the
node's position (`positionOf`, which refuses a node no tree built), stamps its id, and declares on its
state.

The per-pass `Map` survives only as an index from the stamped id to the state declared in this pass,
cleared by the commit. Ids are never given twice, so it cannot hand one position's declaration to another.

*How does Flutter solve it?* A `ScrollPosition` belongs to the `Scrollable`'s `State`, and a replaced
element brings a new one. The DOM element is the browser's render object, but the reconciler matches it
by tag and place, so it can outlive the node it was drawn for. The state therefore records the DOM element
it adopted, and an element another state adopted counts as handed over: the scroll view scrolls it to its
own offset, or to the top. The observers belong to the state and watch the element it adopted. When the
position leaves, `NodeElement.OnDeactivated` disposes its state.

Keying the DOM element by the position's id would also give a replaced node a fresh element. It was not
chosen: an adaptive arm the position does not hold is built through a structural owner each render, and
its keyed elements would then be recreated on every render.

### One count of ids per document

`ElementIds` is the count a tree takes its ids from. `BuildOwner` takes one as an optional parameter
(the twin is JavaScript, so it is not an overload) and makes its own when none is handed. The browser
hands one count to every owner of the document: page roots, islands, and the structural owner of a
lowering with no page around it. Ids then never repeat in a document, even across a navigation, which
keeps `Element.Id`'s promise that an id is never given to another.

*How does Flutter solve it?* A semantics node's id comes from one count for the whole process.

### What stays

- A scroll view that declares nothing (no offset, no request, no viewport callback) is not stamped and
  has no after-pass, as before. Its DOM element is the reconciler's to match.
- The table's DOM-bound expandos (its listeners, its animation frame, the pointer and Tab state) stay on
  the DOM element. They act for whichever position drives it now (`__eqTableState`).
- The child-path threading the lowering still passes after E3b-1 retained instances. With the stamps
  moved, nothing reads it, so it goes.

## Risks

- A lowering of a node no tree built now throws. E3b-1 builds every tree the browser lowers through an
  owner, including an unheld adaptive arm and a lowering with no page around it, so a throw here means
  the engine and the lowering disagree. That should fail loudly, as `ElementFrame.at` does.
- #1464: eqc translates `state as IDisposable` as a null check, so `OnDeactivated` calls `dispose()` on
  any state in the browser. Every browser node state has a `dispose`.
