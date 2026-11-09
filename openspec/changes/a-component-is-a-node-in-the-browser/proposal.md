# Proposal

Part of #1301 (E3a-3), the third slice of #1127, in #1093 (E3). Its first part, in a pull request of
its own, because it changes how the browser builds every component without state.

## Why

The element engine is written in C#, where a component IS a node: `UiComponent : VisualNode`, its kind
`component`, as a Flutter `StatelessWidget` is a `Widget`. The engine's position for a component is a
`ComponentElement` whose configuration is that node. In the browser the two lived in separate class
hierarchies: only `StatefulComponent` carried `nodeKind = 'component'`, and `StatelessComponent`, the
base a transpiled component without state extends, carried none, on purpose. The lowering recognised it
by that absence and let it render itself: its own render context, its own instance store, its own
walk. So the engine, transpiled into the runtime, could not hand a component to the position it
builds (`TS2345: Argument of type 'Component' is not assignable to parameter of type 'VisualNode'`),
and a stateless component built through a second door that no C# realizer has.

## What Changes

- The runtime gains `UiComponent`, the C# one's twin, between the web `Component` and the stateless
  and stateful kinds. It carries the node's members: `nodeKind = 'component'` at run time, `key`,
  `bookmark`, `alignSelf`, `gridSpan`, `build`, `getVirtualNode`. The runtime exports this class as
  `UiComponent`, where it exported an alias of `Component`.
- `HtmlElement`, the DOM escape hatch, stays a `Component` with no `nodeKind`, as in C#, where it is an
  `IComponent` and no node, and keeps rendering itself through the lowering's mixing seam.
- A component without state takes the lowering's `component` arm, as one with state does: the
  positional store, the server data its `Type#ordinal` names, and the error boundary. A lowering handed
  no component context builds it with the ambient one, as its own render did.
- `core/component-is-a-node.type-test.ts` holds `UiComponent` to every member the vocabulary's
  `VisualNode` declares: one the node gains that the component lacks fails `tsc`.
- The list of members the runtime's component already has, which eqc reads to refuse a C# member that
  would shadow one, gains `key` and `nodeKind` for a component without state.

For someone using the SDK nothing changes in what they write. A component without state is named,
contained and retained through the same door as one with state, and a member of theirs named `key` in
a component without state is now refused at build time, as it already was in one with state, because
it would shadow the node's key.

Parts reached: the runtime (`core/component.ts`, `shared/lowering.ts`, the two export files, three
specs and a type test), and eqc's embedded list of runtime members.
