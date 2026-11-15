# Design

## Context

Measured on the slice's base (E3b-2's branch, c1d06dfb7):

- `renderPage` answers a render its owners asked for (`takeReuse`) by lowering the page's last tree again
  through `lowerVisualNode`: the owner builds the marked positions, and then every node of the page lowers,
  and `RenderManager.update` reconciles the whole page.
- The lowering's parents act on a child's root after it lowers: about twenty places merge a class, set an
  attribute or add a handler on it (`prependClass`, `mergeAtomicDeclaration`, a presence's exit marker, a
  draggable's handlers, `decorate`'s key, id and origin). Some read it first (`atomicDeclaration`,
  `asksForAll`, a shortcut joining its marker), and three walk into its subtree (`numberItemRows`,
  `liftThroughBoxlessWrappers`, `fillsViewport`).
- The registries a pass commits: E3b-2 moved the scroll, table, in-view and canvas after-passes onto
  positions, so a subtree not lowered keeps its state. Shortcuts are the one registry still replaced whole
  by each pass (`commitShortcuts`). Focus traps, camera streams, the anchor offset and the scrolled sync read
  the DOM after the pass.

## Decisions

### What a component lowered to is the position's

*How does Flutter solve it?* An element that rebuilds updates its own render object, and the render tree
around it is not rebuilt. The browser's render object is the DOM, and between the element and the DOM stands
the lowering, which writes a node's element from the node and from what its parents decide. So the position
keeps a record: the root its lowering returned, a snapshot of that root before any parent acted on it, its
inputs (the context without the frame, the axis, the path, and the walk's ambients: the container's place
rule, the simulated state, the control flags, the adaptive gates), and its parent record.

The record is taken in `lowerNode` for a component's node, after `decorate` writes the component's own key,
so the snapshot is everything the component's own lowering made.

### Its parents' changes are done again, or the record is not patched

Rather than make twenty call sites declare their change before the child lowers, the change is read off the
two states of the root: what the snapshot lacks and the drawn root has. It is replayed on the root a lowering
alone makes: an attribute written or taken away, a handler set, a key, and classes in the two ways the
lowering adds them (semantic in front and atomic as a sorted set, or the child's classes as one run inside the
parent's). The replay is CHECKED: replaying it on the snapshot must give back the drawn root exactly, or it is
not used. Its handlers are the parents' closures from the last whole render, and the parents did not build
again, so they are the closures a whole render would make.

A parent's change that depends on what the child lowered to cannot be replayed from a difference, so every
read of a lowered child marks its record (`reachedInto`): `atomicDeclaration`, `asksForAll`,
`numberItemRows`, the shortcut's marker. The walks read through the same calls. A marked record is patched by
its parent record, and the page itself is the last resort, which lowers whole.

### A render the owners asked for is answered by patches

`patchPage` runs where the scheduled render ran, before it, for a page whose last build is a vocabulary tree
lowered as one root. Inside the page's walk and pass, `relowerBuilt` builds the root through its owner,
collecting the positions it builds (`noteBuilt`, from the owner's build callback), picks each one's
patchable record, keeps the topmost, and lowers each alone with a fresh frame. `RenderManager.patch` finds the
drawn root in its current tree by identity, the DOM by the same child indices the reconciler uses, reconciles
that subtree alone and puts the new root in the tree. Any refusal leaves the render to `render()`, which lowers
the page whole; the owner's build is not repeated, since nothing is marked any more.

### Shortcuts belong to the lowering that declared them

A binding carries the record whose lowering declared it (`within`). A pass that lowered some components alone
commits by putting their new bindings where their old ones stood, and keeps every other binding. A whole pass
replaces the list, as before.

## Risks

- A parent change that depends on its child's content, made through a path no read marks, would replay an
  old answer. The reads that exist today are all marked, and the specs compare every patched DOM with the DOM
  a whole lowering leaves.
- A page with exit motion keeps a departing layer in the DOM, so the child index the reconciler uses no
  longer matches the tree. The patch uses the same index the reconciler uses, so it is wrong exactly where a
  whole render already is.
