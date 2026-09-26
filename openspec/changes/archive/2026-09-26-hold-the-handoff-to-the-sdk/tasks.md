# Tasks

## 1. Hold the handoff to the SDK

- [x] 1.1 Mark every figure a page derives from a token with its path, and compare it (`HandoffFigureTests`)
- [x] 1.2 Give every request in `status.json` a probe, checked both ways (`HandoffStatusTests`)
- [x] 1.3 Require every public token to be published or exempt (`HandoffSdkCoverageTests`)
- [x] 1.4 Check: the handoff and design token suites of `eQuantic.UI.Native.Engine.Tests` pass (77 tests)

## 2. APCA as a second gate

- [x] 2.1 Add `DesignTokenApcaTests`, with the formula checked against its reference values
- [x] 2.2 Re-solve the dark palette to clear the floors, and regenerate the dark goldens, the web's cross-pins, the generated design system and the shared fixtures through their own switches
- [x] 2.3 Check: every pair clears its floor in both themes, and the goldens match on every backend

## 3. The decisions

- [x] 3.1 Record Edgar's confirmation of 2026-09-26 in `tokens.json`, `proposals.json`, `status.json` and the pages
- [x] 3.2 Open #430 for the 24dp floor, and record the corners decision on #346
- [x] 3.3 Check: the handoff suites still pass with the new text, and every `data-token` marker survived the edit

## 4. Documentation

- [x] 4.1 One `docs/LEDGER.md` line citing #337
- [ ] 4.2 The wiki's design system section names the APCA gate, in English and Portuguese in one commit, published when this merges
