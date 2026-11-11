- **2026-11-11 · The server draws a write-once page through an owner for the request**: each
  prefetch round was a page built anew, with the loaded fields written onto the fresh instance by
  name. The request now keeps a structural `MarkupHost`, each round is a frame of one tree, and only
  the positions that loaded build again, with the instance that loaded. The rounds are one
  implementation the component tester runs too (`ServerDataRounds`), and `MarkupHost` replaces the
  provisional `WebRealizer.LowerFrame`, writing hydration's unmeasured mark from a measurer that notes
  which build asked. The SSR fixtures and the hydration payload are unchanged
  ([#1403](https://github.com/eQuantic/equantic-ui/issues/1403), under
  [#1094](https://github.com/eQuantic/equantic-ui/issues/1094)). Proposed and archived through
  OpenSpec (`openspec/specs/element-tree`).
