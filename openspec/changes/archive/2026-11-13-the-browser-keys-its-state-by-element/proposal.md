# Proposal

Closes #1454 (E3b-2), the second slice of #1128 (E3b), in #1093 (E3), a sub-issue of #937 and of the
1.0 epic #898. The design is `docs/ELEMENT-TREE-PLAN.md` section 3.7, the browser's line: the path
attributes and the module maps become element ids and element state. It builds on E3b-1 (#1453), which
lowers every tree the browser draws from a frame of positions.

## Why

The browser's lowering names a node by a string it forms from the node's path, and keeps state by that
string or on the DOM element the node was drawn on. Both are handed on: a node that takes another's path,
or the DOM element the reconciler gives it, inherits a scroll offset, a scroll request already
honoured, an `InView`'s observer and last report, a canvas's observer, and the code editor's last reveal.
The code editor's revealed versions sit in a module map that nothing clears. Photon keeps all of these on
the node's position (E2), and the browser now has positions too (E3b-1).

## What Changes

- The seven attributes that carry a path today (`data-eq-code`, `data-eq-sheet`, `data-eq-table`,
  `data-eq-scroll`, `data-eq-canvas-fill`, `data-eq-inview`, `data-eq-focus-scope`), and the ids and
  keys formed from one (the code surface's options list and its input's key, an anchored panel's id, a
  canvas's key), carry the integer id of the node's position. It is assigned at mount and kept while the
  position lives.
- The scroll, table, in-view and canvas after-passes keep what they remember between passes on the
  position's state (`NodeElement.State`): the latest declaration, the DOM element it adopted, what it
  did there once, and its observer. The position lets go of the observer when it leaves the tree. A DOM
  element handed from one position to another starts at the new position's offset.
- The code editor's revealed version lives on the surface's position and leaves with it.
- Focus within a scope walks the focused element's parents for the scope's id.
- The element engine takes its ids from a count it can be handed (`ElementIds`). The browser hands one
  count to every owner of the document, since a page owns a tree per root and two roots counting alone
  would give two nodes the same id. Photon, the server and the component tester keep a count per owner.

For someone using the SDK: nothing they write changes. A scroll view, an `InView`, a canvas or a code
editor that takes another's place in the tree starts as itself.

Parts reached: the element engine (`src/eQuantic.UI.Primitives/Elements`, its transpiled modules), the
runtime (`shared/lowering.ts`, the four after-passes, `shared/node-states.ts`), its specs. The public
surface moves in Primitives: `ElementIds` arrives, and `BuildOwner`'s constructor and `Structural` take
an optional count. A call with one argument compiles as before. No developer-surface line moves.

Principles followed: the structural fix over the patch (state on the position, not a reset when a path
repeats); Flutter's answer, where a replaced element brings a new render object and a new scroll position.
