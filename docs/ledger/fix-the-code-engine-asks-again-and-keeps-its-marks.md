- **2026-11-11 · The code engine asks again past a complete answer, and keeps a problem at a line's end**:
  a complete answer was filtered for a word it never covered, so ⌃Space at the end of `Col` and Backspace
  left out what the provider offers for `Co`. The completion now asks again, as the list opened, each
  provider whose complete answer the word no longer extends
  ([#1016](https://github.com/eQuantic/equantic-ui/issues/1016)). A problem of no width went with a line
  written right after it, at the document's end by a move and anywhere by Enter at a missing semicolon. An
  empty range now stays in front of an insertion that begins with a line break
  ([#1059](https://github.com/eQuantic/equantic-ui/issues/1059)). A diagnostics test waited for its answer by
  waiting for its queue to hold anything, and the queue already held the folding's first answer: it now
  drains that first. A probe saw the answer's continuation come after the await in 1 of 300 runs, where
  the old wait ended without it, and the fixed test passed 100 of 100 runs under load ([#1298](https://github.com/eQuantic/equantic-ui/issues/1298)).
  Proposed and archived through OpenSpec (`openspec/specs/code-completion`, `openspec/specs/code-diagnostics`).
