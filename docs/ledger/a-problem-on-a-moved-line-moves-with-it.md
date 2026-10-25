- **2026-10-25 · An undo step holds the edits one command made, and a moved line keeps its problems**: a
  line operation of #1033 was one replacement over every line it rewrote, so `CodeEdit.Carry` took a
  problem on a moved line to the replacement's ends, over the moved line and the one it passed. A step of
  the history is now the edits one command made, in order: undo takes them back last first, redo makes them
  again, and the editor raises `Changed` once the step is made, with every replacement in the order made, a
  `CodeChange`, where it raised one `CodeEdit` or null. The editor's problems follow each replacement the
  moment the document takes it, as its colours do, so one reported at any moment stands where it was
  reported.
  A move is the line it passes written on the block's other side and deleted where it was, so the block's
  problems travel with it, through the move, its undo and its redo
  ([#1031](https://github.com/eQuantic/equantic-ui/issues/1031)); one of no width at the document's end
  is [#1059](https://github.com/eQuantic/equantic-ui/issues/1059). The history's twin met a transpiler gap
  on the way: a method's outs were declared untyped in TypeScript, and one assigned an empty list failed
  `tsc`; eqc declares them `any` there now
  ([#1034](https://github.com/eQuantic/equantic-ui/issues/1034)). Proposed and archived through OpenSpec
  (`openspec/specs/code-history`, `openspec/specs/code-lines`, `openspec/specs/code-editor`).
