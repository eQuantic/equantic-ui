# Tasks

## 1. The control

- [x] 1.1 Compare the live value with what the page holds, for an input and a textarea with an `input` handler
- [x] 1.2 Prove it both ways: `text-input.spec.ts` fails for a reset in the same render and for a multiline reset against the reconciler as it stood, and passes; a field with no handler keeps what was typed on both sides

## 2. The grip (#1092)

- [x] 2.1 Keep the keyboard on the sheet through a press on a grip drawn inside it: focus the text door on the `pointerdown`, prevent the `mousedown`
- [x] 2.2 Prove it both ways: `spreadsheet-pointer-web.spec.ts` fails against the lowering as it stood and passes
- [x] 2.3 Prove it in Chromium on the dashboard's `/sheet`: the focus stays on the sheet through a resize, and ⌘Z takes it back

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` per Bug, citing #1026 and #1092; the wiki states no gap here, so no page changes
