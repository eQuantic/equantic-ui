- **2026-11-09 · A component is a node in the browser too**: the runtime gains `UiComponent`, the C#
  one's twin, between the web `Component` and the two kinds, carrying the node's kind at run time and
  its members, held to the vocabulary's `VisualNode` by a type test `tsc` reads. A component without
  state, which carried no kind on purpose and rendered itself through the lowering's mixing seam, now
  takes the `component` arm one with state takes: the positional store, the server data its ordinal
  names, the error boundary. No ordinal moves (a mixed page and a client navigation pinned, both
  passing with the old routing put back), the boundary contains what it contained, and `HtmlElement`
  stays outside, as in C#. The first part of E3a-3, which lets the element engine hand a component to
  the position it builds ([#1301](https://github.com/eQuantic/equantic-ui/issues/1301)). Proposed and
  archived through OpenSpec, adding a requirement to `element-tree` and one to `hydration-contract`.
