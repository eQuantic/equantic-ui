- **2026-10-14 · The Element and the theme slots come before 1.0**: Edgar took the three decisions
  1.0 waited on. The Element is built before the release, the spreadsheet's engine leaves Primitives
  as the code editor's did, and per-component theme slots ship in it
  ([#169](https://github.com/eQuantic/equantic-ui/issues/169),
  [#185](https://github.com/eQuantic/equantic-ui/issues/185),
  [#186](https://github.com/eQuantic/equantic-ui/issues/186)). `docs/ELEMENT-TREE-PLAN.md` designs the
  first and the last together, since a slot resolved per component needs a theme found per position,
  on an inventory re-measured that day: four spellings of identity, six path-keyed stores of twelve
  dictionaries and a set, twelve `AsyncLocal` channels, and a frame that builds the whole tree on any
  `SetState`. Its six slices are two features under the 1.0 epic
  ([#937](https://github.com/eQuantic/equantic-ui/issues/937),
  [#938](https://github.com/eQuantic/equantic-ui/issues/938)). The measurement found two defects that
  became their own bugs: a stateful root gains a handler on every Photon frame (101 after 100,
  [#935](https://github.com/eQuantic/equantic-ui/issues/935)), and a theme's code palette never
  reaches the browser ([#936](https://github.com/eQuantic/equantic-ui/issues/936)). The audit records
  the spreadsheet's road and the slots' shape and order.
