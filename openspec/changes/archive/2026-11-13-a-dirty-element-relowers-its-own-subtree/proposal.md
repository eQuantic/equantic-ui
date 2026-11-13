# Proposal

Closes #1455 (E3b-3), the third slice of #1128 (E3b), in #1093 (E3), under #937, in the 1.0 milestone by
the maintainer's decision. It stacks on E3b-2 (#1454), which made the browser's attributes element ids and
its after-pass state element state, and on E3b-1 (#1453), which builds the browser's trees through the
element engine.

## Why

After E3b-1, a nested component's `setState` builds only its own element, but the page it sits in still
lowers all of itself from the frame, and the reconciler then reads the whole page. The cost of a click on a
counter in a page of a thousand rows is the thousand rows.

## What Changes

- Each component's position keeps what it lowered to and what it lowered with (`lowered-records.ts`): the
  root as its own lowering left it, the same root as the page drew it after its parents acted on it, its
  context and the walk's ambients, and the record of the component above it.
- A render the page's owners asked for (only marked positions) builds those positions, lowers each
  component that built alone, does again what its parents did to its root, and patches only the DOM that
  root drew (`RenderManager.patch`). The page's own build does not run, and its other components are not
  lowered.
- A parent's change is done again only where it can be said exactly: replaying it on the root the
  component's own lowering left must give back the root the page drew. A component a parent read or walked
  into (a shortcut's marker, a box sized from its child, an anchored menu's numbered rows) is lowered with
  the nearest record above it that nothing reached into, the page in the end, which lowers whole as before.
- The shortcuts the components lowered again had declared give way to the ones they declare now, and every
  other binding stays.
- A page that lowered more than one root (a Core page's bridges) or whose build is a Core tree lowers whole,
  as before.

For someone using the SDK: nothing they write changes. A component's state change costs that component's
subtree, and the DOM it leaves is the DOM a whole-page render leaves.

Parts reached: the runtime (`core/component.ts`, `dom/renderer.ts`, `dom/shortcuts.ts`,
`shared/element-roots.ts`, `shared/element-engine.ts`, `shared/lowering.ts`, `shared/style-atomizer.ts`, the
new `shared/lowered-records.ts`), its specs. No public C# surface moves, and no developer-surface line.

Principles followed: the structural fix (a position keeps what it lowered to) over a cache of the page's
markup; an instrument that refuses (a replay that cannot give back what the page drew is not used) over one
that guesses.
