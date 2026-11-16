## Context

Photon's semantics walk (`SemanticsVisitor`) is a flat list in reading order: a node that announces
CONSUMES its subtree, a container announces and keeps walking. The web writes ARIA inline as it lowers, and
`ComponentTester` reads it back the way a browser's reader does (`AnnouncedTree`). `SemanticsParityTests`
compares the two readings for every component of the catalog. `Text.ExcludeFromSemantics` already hides one
paragraph on both targets, unless a run of it links somewhere.

The browser does not lower a page again when a component inside it rebuilds: it lowers that component alone
and patches its DOM (`relowerBuilt`), unless an ancestor reached into what the component lowered
(`reachedInto`).

## How Flutter solves it

`MergeSemantics` merges the semantics of its descendants into one `SemanticsNode`: labels are joined in
order, flags and actions are combined, and a tap on the node reaches the descendant that handles it.
`ExcludeSemantics(excluding: true)` drops its subtree from the semantics tree, whatever it holds, and keeps
the subtree's own behaviour. Both are widgets that wrap a child and paint nothing. This change takes both
names and both shapes.

## Decisions

1. **Two nodes, layout-transparent, the `Group` precedent.** Each is a `SingleChildNode` with a host of its
   own on the web, which takes the child's fill and cap, and `MeasureWrapper` on Photon. A wrapper is the
   only shape that can hold an arbitrary subtree, and the host is what a reader outlines for a merged group.

2. **A merge is ONE stop, and a control in it is that stop.** Photon collects the subtree's stops with a fresh
   walk and joins their names with a space. The first stop whose role a person operates (`IsControl`: button,
   link, field, slider, checkbox, switch, grid cell, radio, tab, menu item, option, destination, combobox, tree
   item) gives the stop its role, value, states and identity, so the bridge's activate presses it; its own
   name takes its place in the joined one. A later control stays a stop of its own, after the merged one: one
   stop cannot be pressed as two things. With no control the stop is a `SemanticRole.Group` at the merge's
   identity. The stop has the merge's bounds, as Flutter's merged node has. Alternative refused: joining the
   controls into one name, which leaves a stop a press cannot resolve.

3. **On the web the name is the browser's to compute.** The lowered subtree is cut into the parts a reader
   would have stopped on: an element holding only text, one named by a label, an image, an element with a
   role that does not group. A layout element around them is walked into, so the parts of a row are named
   apart and the reader hears a word between them, as Photon joins its stops. Each part with something to
   say gets an id and `aria-hidden`; the control's `aria-labelledby`, or the host's as a `role="group"`, lists
   them in reading order, the control's own id in its place. A part named by `aria-labelledby` is read however
   hidden, so the name follows the content with nothing written into a string. Alternative refused: an
   `aria-label` written from the subtree's text, which goes stale the moment a component inside rebuilds alone.

4. **The parts are found from the markup, so the browser's walk reaches into it.** `mergeReading` calls
   `reachedInto` on every element it visits, so a component inside a merge is never lowered again without the
   merge around it, and the ids and hidden marks are written again with it.

5. **Ids from the path scope.** `eq-merge-{scope}-{n}` and `-c` for the control, where the scope is the
   merge's path hashed by `LoweringPath.Scope` / `pathScope`, the rule the grids' cell ids follow (#1115): the
   same on the server's first pass and after hydration, unique per merge on a page. An element that already
   has an id keeps it.

6. **An exclusion hides everything, controls included.** As Flutter's does and as `aria-hidden` does. The
   rule `Text.ExcludeFromSemantics` keeps for a linked run was refused for a subtree: what it holds can change
   when a component inside rebuilds alone, and the host's answer would go stale. The node's documentation
   says to wrap no control in it. `Excluding` keeps the node in the tree when it turns off, so nothing inside
   loses its state.

7. **The harness reads `aria-labelledby` as accname does.** An element naming itself among its references is
   named as it would be with no reference (step 2B, once), and a referenced node gives its `aria-label` or
   `alt` before its text. Its old reading took a referenced node's text only, which nothing exercised until
   now: the realizer wrote no `aria-labelledby` before this change.

## Risks / Trade-offs

- A run of text standing directly beside an element in a layout element cannot carry an id, so it is not a
  part. The realizer writes text inside elements, so only an escape hatch meets it.
- An `AdaptiveNode` inside a merge lowers every arm on the web, and its hidden arms' parts are named too.
- A merge's static stop is a group, where Flutter's has no role: the web has no named text without a role,
  and the two targets say the same word.
