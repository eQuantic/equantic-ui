- **2026-11-11 · The browser keeps a node's state on its position**: the seven attributes the browser found a
  node by after a render carried the node's lowering path, and what the after-passes remembered (a scroll
  view's applied offset and honoured request, an `InView`'s observer and last report, a canvas's observer,
  a table's last report, the code editor's last reveal) lived in module maps keyed by it or on the DOM
  element the reconciler reuses, so a node that took another's place inherited it. Now the attributes carry
  the integer id of the node's position, the state lives on the position and leaves with it, and the
  engine takes its ids from a count every owner of a document shares (`ElementIds`), so two roots never
  give one id twice ([#1454](https://github.com/eQuantic/equantic-ui/issues/1454), E3b-2 of
  [#1128](https://github.com/eQuantic/equantic-ui/issues/1128)). Found on the way: eqc tests an interface
  as a null check ([#1464](https://github.com/eQuantic/equantic-ui/issues/1464)). Proposed and archived
  through OpenSpec (`openspec/specs/element-tree`).
