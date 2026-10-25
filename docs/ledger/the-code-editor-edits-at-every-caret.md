- **2026-10-25 · The code editor edits at every caret**: an editor held one selection, so a word renamed
  in five places was five edits and a column of values could not be typed into at once. It holds several
  now, the primary the one placed last, as Rider's is: ⌥⌘↑ and ⌥⌘↓ add a caret above or below every
  caret, ⌘D the next occurrence of the word, ⇧⌘L every one, ⌥ and a press adds or takes away a caret,
  and ⇧⌥ and a drag select a column; carets that meet are made one, as VS Code merges its cursors. Every
  command runs at every caret as one undo step, from the last caret to the first, the carets placed in
  one pass linear in their number; the line operations run once for each block of carets, and Tab, ⇧Tab
  and ⌘/ over lines are an edit a line, so a problem on an indented line keeps its text. A paste spreads
  a line to each caret, a completion goes in at every one, and undo and redo put back every caret
  ([#1012](https://github.com/eQuantic/equantic-ui/issues/1012)). The code block's twin met a transpiler
  gap on the way: an empty list of tuples was left unannotated in TypeScript and inferred as an array of
  arrays; eqc annotates it with the tuple now ([#1063](https://github.com/eQuantic/equantic-ui/issues/1063)).
  Proposed and archived through OpenSpec
  (`openspec/specs/code-cursors`, `openspec/specs/code-history`).
