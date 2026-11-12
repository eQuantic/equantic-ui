# Proposal

Closes #1453 (E3b-1), the first slice of #1128 (E3b), in #1093 (E3), a sub-issue of #937 and of the 1.0
epic #898. The design is `docs/ELEMENT-TREE-PLAN.md` section 3.7, the browser's line: the runtime keeps
the same element tree Photon keeps, and hydration builds it.

## Why

E3a-3 (#1405) brought the transpiled element engine into the runtime, and nothing in the browser drives
it. The browser keeps its stateful instances in a `ComponentInstanceStore` per page, keyed by a lowering
path (`r0/1/0#Type#key`), and builds every component again on every render: a nested component's
`SetState` calls its page's invalidator, and the page builds and lowers all of itself. The C# web
realizer already lowers a frame of a live tree from its positions (`ElementFrame`, #1245), and Photon
builds through the same engine every frame. The browser is the last target that does not.

## What Changes

- A page root, and an island rendered outside a page, owns the engine's `BuildOwner`s in place of a
  `ComponentInstanceStore`, one per root its render lowers, as the store kept one identity space per root.
  An island rendered inside a page joins the page's, as it joins the page's store today.
- `lowerVisualNode` builds its root through the owner, then lowers what each position built. The
  lowering's component arm and the stack's resolution of a component child read the instance a position
  keeps and what it built, from a frame of the tree; they no longer reconcile or build. A lowering with
  no page around it builds through a structural owner and discards it, as the server does.
- A component's build runs inside the owner's build callback, which keeps today's boundary (a throw
  costs the component's own subtree) and hands a fresh instance what the server loaded for its position
  before it builds.
- **Every arm of an adaptive node is a position on the web** (#1477): the web's owners hold every arm
  (`BuildOwner(…, holdsEveryArm: true)`), so no window size class decides an arm there. `MarkupHost.Build`
  loses its size class, and the frameless arm, in C# and TypeScript, goes.
- **Server data is keyed by position** (#1477): `r{tree}/{type}@{slots}`, read off the element on both
  sides (`Element.Slot`, `Element.SlotsBelow`, `BuildOwner.Building`), in place of `Type#ordinal`, whose
  build order and lowering order parted once the browser built through the engine. A page's own
  component keeps `Type#0`. The server names by it as it lowers (`ComponentExpansionScope.Name`), draws
  an escape-hatch bridge through a host of its own (`MarkupHost.DrawOnce`) and puts back what loaded
  before a fresh instance builds (`PutBack`); `Enter` and `Reserved` go, and `MarkupHost.ForPage` measures
  a page's keys below the page.
- A nested component's `SetState` marks its own element. The owner asks the page for a render, and that
  render lowers the tree the page last built, without building the page again, so the owner builds the
  marked element and nothing else. Any other render (the page's own `SetState`, a navigation, a theme,
  density or culture change, hot reload) builds the page as today and walks every position.
- Mounting and unmounting come from each owner's `EndFrame`, at the point the store's pass ended them.
  A page leaving takes every owner down (`UnmountAll`).
- Hydration renders as above, so the element tree exists before the reconciler walks the server's DOM,
  in the order it walks today.
- `ComponentInstanceStore`, its build-root hops and the instance's invalidation hook go.

The path attributes, the per-commit module maps and the identities derived from a path stay as they are:
they move in E3b-2 (#1454).

For someone using the SDK: nothing they write changes. A nested component's state change no longer runs
the `Build` of every other component on its page, and a component composed in two arms of an adaptive
node, or inside an overlay, keeps the data the server loaded for it. The hydration payload's keys change
shape, which only a hand-written payload would notice. For a test that called
`MarkupHost.Build(root, sizeClass)`: drop the size class.

Parts reached: the runtime (`core/component.ts`, `shared/lowering.ts`, `shared/instance-store.ts`, which
goes, `shared/visual-node-component.ts`, a frame of the tree beside the lowering), its specs, and the
served runtime's budget; the element engine (`Element`, `NodeElement`, `ChildSlots`, `NodeSlots`,
`BuildOwner`, regenerated into the runtime, with its twins); the web realizer (`MarkupHost`,
`ElementFrame`, `ComponentExpansionScope`, `VisualNodeComponent`, `WebRealizer`), the server's rendering,
the component tester, and the tests that read a payload's keys.
