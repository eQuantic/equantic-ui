- **2026-10-19 · The code editor explains the call being written, and its completion waits for no
  provider**: the rest of slice 4 of `docs/CODE-EDITOR-PLAN.md`. `CodeSignatureHelpSession` opens the
  panel on a provider's trigger character or ⌘⇧Space, asks again while it shows, after 120 ms as VS Code
  does and at once on a retrigger character, and lets its providers (`ICodeSignatureProvider`, LSP's
  `textDocument/signatureHelp`, typed) say which signature applies and which argument the caret is on,
  `gap:` included, and close it by answering none. ↑ and ↓ walk the overloads, and Escape closes it.
  `CodeEditor.SignatureProviders` draws it as a note of the code surface over the caret's line, on the
  other side of a completion list, the argument marked in the accent colour, a polite live region. The
  completion applies each provider's answer as it arrives, in the order of `Providers`, keeping the
  entry selected, and a request belongs to its provider, so asking one again no longer drops what a slow
  one still owes ([#298](https://github.com/eQuantic/equantic-ui/issues/298),
  [#620](https://github.com/eQuantic/equantic-ui/issues/620)). Proposed and archived through OpenSpec
  (`openspec/specs/code-signature-help`, a new spec, and `openspec/specs/code-completion`).
