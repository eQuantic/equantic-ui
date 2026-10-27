- **2026-10-27 · The code editor folds its regions**: the engine had a fold contract and a row map that hides
  lines under a header, but nothing asked the contract, kept what was folded or drew a way to fold. A chevron
  beside the line numbers now marks every line a region starts on, down while open and right while folded, a
  button for a screen reader named by its lines, and a press toggles it; a folded header shows `⋯` after its
  text, which unfolds it, and the caret steps over the lines it hides. `CodeEditorController.Folding`
  (`CodeFolding`) asks `ICodeFoldProvider` (LSP's `foldingRange`, now asynchronous) once the text rests, or the
  indentation's provider when there is none, and joins the answers as VS Code's `sanitizeAndMerge` does. VS
  Code's seven keys fold and unfold in both conventions (⌥⌘[ ⌥⌘], Ctrl+Shift+[ ], ⌘K ⌘[ ⌘K ⌘] ⌘K ⌘L ⌘K ⌘0
  ⌘K ⌘J), and a fold never hides what is worked on: the regions are carried through every edit, an edit or a
  caret reaching a hidden line unfolds it, a fold over a caret puts it at the end of the header, and an answer
  never folds a region over a caret, as VS Code's folding model blocks those lines
  ([#1119](https://github.com/eQuantic/equantic-ui/issues/1119)). Proposed and archived through OpenSpec
  (`openspec/specs/code-folding`).
