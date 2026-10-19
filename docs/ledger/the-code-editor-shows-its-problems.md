- **2026-10-19 · The code editor shows its problems and explains the code**: slice 4 of
  `docs/CODE-EDITOR-PLAN.md`, its problems and its hover. `CodeDiagnostics` holds what the app reports
  and what its providers answer, asks them once typing pauses with one request out at a time, and
  carries every problem through each edit (`CodeEdit.Carry`; undo and redo now raise the edit they
  make) until whoever reported it reports again. `CodeHoverSession` waits for a pointer to rest, or
  answers ⌘K ⌘I and F8 at once, and holds the card's problems and answers. `CodeEditor` draws a
  problem as a wave in its severity's colour, or three dots for a hint, marks a line's worst error or
  warning in the gutter, and draws the card as a note of its code surface (`CodeSurface.Notes`), a
  polite live region when a key brought it up; both hosts report a pointer at rest to the model.
  `Task.Delay` waits in the browser as .NET waits, its cancellation and its argument checks included.
  Found on the way: an answer that found the same problems made a parent that builds on them build
  again without end, the three new properties never reached the editor a position keeps, and on the
  web the card F8 brought up stood where the view had been before the code slid to the caret
  ([#299](https://github.com/eQuantic/equantic-ui/issues/299),
  [#1005](https://github.com/eQuantic/equantic-ui/issues/1005)). Proposed and archived through
  OpenSpec (`openspec/specs/code-diagnostics`, `openspec/specs/code-hover`, new specs).
