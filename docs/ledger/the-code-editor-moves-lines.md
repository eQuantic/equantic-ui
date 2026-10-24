- **2026-10-24 · The code editor moves, copies, deletes, joins and sorts lines**: the second part of
  slice 2 of `docs/CODE-EDITOR-PLAN.md`. ⌥↑ and ⌥↓ move the lines the selection takes, the selection
  with them, ⇧⌥↑ and ⇧⌥↓ copy them, ⇧⌘K deletes them, ⌃J joins them as VS Code joins (the next line's
  leading spaces and tabs go, and one space joins the text), ⌘↩ and ⇧⌘↩ open a line below and above
  the caret's, and two commands no key runs sort them, case aside and then by code unit, the same on
  both targets. Each operation is one replacement and one undo step, and the history records the
  selection it leaves, so redo selects the lines again. Compared with the twin over the joins of the
  white space .NET and JavaScript disagree on, the sorts of lines whose order only case decides, and
  seeded sessions on a real file (`CodeLinesTwinTests`): the joins fail against a twin that trims with
  JavaScript's own `trimStart`, and the sorts against one that orders by its own lower case
  ([#1011](https://github.com/eQuantic/equantic-ui/issues/1011)). Proposed and archived through
  OpenSpec (`openspec/specs/code-lines`, a new spec).
