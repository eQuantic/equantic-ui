## Context

`docs/ELEMENT-TREE-PLAN.md` 3.9: the engine is written once in C# and transpiled into the runtime.
E3a-1 (#1314) wrote it in what eqc translates; the modules it writes then failed the runtime's `tsc`
for two reasons, six emission bugs of eqc's (#1299, #1303 to #1307) and the browser's component model.
This slice is the model's first half, the one that changes behaviour, in a pull request of its own.

How Flutter answers: a `StatelessWidget` and a `StatefulWidget` are both `Widget`s, and an element is
made for either through the same `createElement`. C# says the same: `UiComponent : VisualNode`.

## Decisions

### A component is a node by what it carries, not by inheritance

The vocabulary's `VisualNode` lives in `shared/` and lowers itself through `shared/lowering`; the
components live in `core/`, which imports nothing of `shared/` at run time (the runtime's module
cycles). So `UiComponent` does not extend `VisualNode`: it declares the node's members, `nodeKind` at
run time and the rest as the node declares them, and a type test that `tsc` reads keeps the two the
same. Extending it would need the node's base moved into `core/` without its lowering, a larger move
that buys this slice nothing.

Rejected: declaring `nodeKind` on the component for the type checker only. The component without state
would then claim a kind it does not carry at run time, and the type test would bless the lie.

### The escape hatch stays outside

`HtmlElement` extends the web `Component` and not `UiComponent`, because in C# it is an `IComponent` and
no node. The lowering's mixing seam keeps recognising it by its missing kind, and that is now the one
thing the seam is for.

### One door for every component

A component without state used to reach the lowering through the seam and render itself: its own
render context, its own instance store and pass, its own walk naming it, its own catch. It now takes
the `component` arm, as one with state does. Measured against each of those:

- **Its name.** Both doors take `Type#ordinal` for it once, before it builds, and the count is per type,
  so no ordinal moves, of either kind. A page that intersperses them, and a navigation onto one, are
  pinned in `server-state.spec.ts` and `ssr-seam.spec.ts`; both pass with the old routing put back, and
  both fail when the arm stops naming a component without state.
- **Its boundary.** C#'s `ComponentBoundary` contains every `UiComponent`'s build. The old door
  contained the throw too, in its own render's catch; the arm contains it with the same surface and a
  report that names the component. Nothing that was caught propagates now, and nothing that propagated
  is caught.
- **Its context.** The arm hands `build` the lowering's component context, which a lowering called with
  a bare context does not have; it falls back to the ambient one, which is what the old door built its
  own from.
- **Its retention.** The arm reconciles it against the positional store, which retains only what has
  state, so a component without state is built fresh, as before. The runtime's retention cases, a
  stateful component inside a stateless wrapper keeping its state across its host's rebuild among
  them, pass unchanged.

### Who imports `UiComponent`

The runtime exported `Component as UiComponent`, and every transpiled module that names the C# type
imports it from there. It now names the real class; a transpiled component extends
`StatelessComponent` or `StatefulComponent`, which extend it, so a value typed `UiComponent` is one of
them, and an `HtmlElement` is not, as in C#.

## Risks / Trade-offs

- [Every component without state of every app takes a new door] → the runtime's suite (2905 cases),
  the three cases above, the server's document tests, and the served runtime's budget.
- [A member named `key` in an app's component without state is now refused] → it shadowed the node's
  key and changed the component's identity; the refusal is the one a stateful component already had.
