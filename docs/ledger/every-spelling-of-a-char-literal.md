- **2026-10-29 · Every spelling of a char literal is measured on both sides**: a task under #849
  ([#1165](https://github.com/eQuantic/equantic-ui/issues/1165)). #527 made a char literal cross from its
  value where it had crossed as its C# token text, and its cases left out two spellings JavaScript reads
  otherwise when handed the token: measured in the embedded Bun and in Node, `'\x007'` is two characters
  and `'\x0041'` three. The conformance suite now runs every spelling of a char on both sides, one case
  each, `'\a'`, `'\e'`, `'\x7'`, `'\x007'`, `'\x0041'`, `'\0'`, a space and a letter, and the five
  JavaScript reads otherwise fail on the token-text arm, each on its own. The translation did not change.
  Proposed and archived through OpenSpec (`openspec/specs/transpiler-expressions`).
