- **2026-11-11 · A constructor's params parameter is the array it is**: a bug under #1174
  ([#1192](https://github.com/eQuantic/equantic-ui/issues/1192)). A constructor's `params` stayed a rest parameter
  after #1159 made a method's an array, so `new Bag(items)` spread the array past an engine's argument cap and the
  constructor got a copy. The site packs what a call lists and passes an array whole as it is, from the bound tree,
  and the twin counts the parameter as the one argument it is, which also lets a `params` constructor stand beside
  another of the same count, refused with EQ1009 before. A type of the SDK's own keeps its rest parameter. Proposed
  and archived through OpenSpec (`transpiler-bcl`, `transpiler-expressions`).
