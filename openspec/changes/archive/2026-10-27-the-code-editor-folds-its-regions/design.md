## Context

Slice 2 left the engine a fold contract (`ICodeFoldProvider.FoldsFor`, synchronous) and an indentation provider,
and slice 2b a row map (`CodeRows`) whose collapses hide lines, with a placeholder row (a diff's unchanged run)
or with none, under a header that stays. The controller already steps over hidden lines, reveals around them and
hit-tests through them, because the diff's modified side is editable. What no part does yet is ask the contract,
keep which regions are folded, or draw anything a person could fold with.

## Goals / Non-Goals

**Goals:**

- VS Code's folding: the gutter's chevrons, the `⋯` after a folded header, and its seven keys in both
  conventions.
- A contract an LSP adapter maps to `textDocument/foldingRange`, asked as every other one is.
- A fold that never hides what the person is working on: edits and the caret reaching inside unfold it.

**Non-Goals:**

- Folding by kind (all comments, all regions, ⌘K ⌘/ ⌘K ⌘8 ⌘K ⌘9) and by level (⌘K ⌘1 to ⌘7), and "fold all
  except": VS Code's later additions, each a follow-up if an app asks.
- Remembering folds across documents or sessions: the app owns its documents.
- Chevrons shown only while the pointer is over the gutter, VS Code's default: here they always show, muted,
  VS Code's `showFoldingControls: always`, since the vocabulary has no hover that reaches a column's children.

## Decisions

- **The contract becomes asynchronous.** `ICodeFoldProvider.FoldsAsync(document, cancellation)` answers
  `CodeFold`s (start line, end line, the collapsed text LSP calls `collapsedText`). In preview a signature widens
  and the old one goes; `IndentationFoldProvider` answers at once.
- **A session on the controller, `CodeFolding`.** It asks every provider at once after the text rests (a delay,
  as the problems wait), joins their answers, sorts them by start and keeps one region per start line (the
  outermost, as VS Code's `sanitizeAndMerge` does), and drops one that crosses another. With no provider it asks
  the indentation's. A late answer is dropped by its generation.
- **Regions and folds follow the text.** Between answers the regions are carried through every edit by line, a
  folded one stays folded, and an edit that changes a hidden line unfolds the region hiding it. A region whose
  header line was removed goes. When the next answer arrives, a region starting on a line that was folded stays
  folded, as VS Code keeps a collapsed state across updates by its start line.
- **What the caret reaches is never hidden.** When the selection moves into a hidden line (a jump, find, go to
  line, a walk), the regions hiding it unfold, as VS Code's hidden range model adjusts selections. A command that
  folds over a caret puts it at the end of the header, as VS Code moves its cursors out of what it folds, and an
  answer never folds a region over a line a selection ends on, as VS Code's folding model blocks those lines.
  Copy and cut take the hidden lines with the selection, which is the document's.
- **A fold is a collapse with no placeholder.** The session answers the `CodeCollapse`s of its folded regions,
  the lines under each header with no row of their own, and `CodeEditor` builds its `CodeRows` from them for the
  grid and the block, as the diff builds its own. Nested folds inside a folded one are covered by it.
- **The gutter carries the chevrons, the line carries the `⋯`.** A chevron sits right of the line number on
  every line that starts a region, down while open and right while folded, a press toggling it; it is a
  button for a screen reader, named by its lines and expanded or not. A folded header shows `⋯` after its text,
  a press on it unfolding the region, as VS Code's inline marker does.
- **The keys are VS Code's.** ⌥⌘[ ⌥⌘] (Ctrl+Shift+[ ]), ⌘K ⌘[ ⌘K ⌘] for the recursive pair, ⌘K ⌘L to toggle,
  ⌘K ⌘0 and ⌘K ⌘J for all, through the keymap's chords of two keys (#1027). A bracket pressed with ⇧ or ⌥ may
  reach the host as the character it types, so the bindings name the key both ways where it does.

## Risks / Trade-offs

- A provider asked after every pause in typing. → The delay and the generation keep it to one request per pause,
  as the problems already do.
- Folds carried by line between answers can be off by a region for a moment after an edit that moves brackets.
  → The next answer settles them; what is folded is kept by its start line, as in VS Code.
