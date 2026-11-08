- **2026-10-30 · The code editor keeps an answer and a mark only as far as they stand**: two Bugs,
  ([#1016](https://github.com/eQuantic/equantic-ui/issues/1016), [#1059](https://github.com/eQuantic/equantic-ui/issues/1059)).
  A complete completion answer was kept for any word, where it promises only the words that extend its own, so a
  word backspaced past it stayed short of entries until the list closed; such a provider is asked again now, as
  `Invoked`. And `CodeEdit.Carry` pushed an empty range past a line written at it, so a problem at the document's
  end left with the line a move passed, and one at a missing semicolon left on Enter; it stays in front of an
  insertion that begins with a line break. Proposed and archived through OpenSpec (`code-completion`,
  `code-diagnostics`).
