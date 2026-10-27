## 1. The contract and the session

- [x] 1.1 `ICodeFoldProvider.FoldsAsync` and `CodeFold`; `IndentationFoldProvider` on the new contract
- [x] 1.2 `CodeFolding` on the controller: asked after the text rests, joined, one region per start line, a late answer dropped
- [x] 1.3 Fold, unfold, their recursive pair, toggle, all; the collapses of what is folded
- [x] 1.4 Regions and folds carried through edits; an edit inside unfolds; the caret inside unfolds
- [x] 1.5 `CodeEditor.FoldProviders`

## 2. Commands and keys

- [x] 2.1 The seven commands in `CodeCommand`, with `CanExecute`; the keys in both conventions and the keymap's pin

## 3. The view

- [x] 3.1 The rows of a folded region in the grid and the block
- [x] 3.2 The chevrons in the gutter, pressable and named; the `⋯` after a folded header; the strings in every language the SDK speaks
- [x] 3.3 Component tests on Photon

## 4. The twin

- [x] 4.1 The new types transpile, and the runtime type-checks and passes
- [x] 4.2 The conformance suite runs seeded sessions of folding on both sides, over the regions of a real file

## 5. Proof and documentation

- [x] 5.1 The dashboard sample's code screen folds, driven in a browser by the gutter, the `⋯` and the keys
- [x] 5.2 The wiki's CodeEditor page in its five languages, docs/CODE-EDITOR-PLAN.md, and a ledger entry citing #1119
- [x] 5.3 The change archived before the merge
