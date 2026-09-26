# Tasks

## 1. The rows

- [x] 1.1 Map a view's lines to rows, with fillers and folds, and place a line on its row
- [x] 1.2 Draw rows in `CodeBlock`: padding, the other document's lines, placeholders, row washes
- [x] 1.3 Step the caret over a fold, draw no band there, leave a press on a placeholder to it
- [x] 1.4 Check: every row where the map puts it on Photon's layout (`CodeBlockRowsTests`)

## 2. The diff

- [x] 2.1 Lay out each side, side by side and inline, with the folds (`CodeDiffLayout`)
- [x] 2.2 Read a unified diff, and open a source from texts, documents or a patch
- [x] 2.3 Compose `CodeDiff`: the two sides, the gutters, the toolbar, the keys, the editing
- [x] 2.4 Check: `CodeDiffComponentTests`, and the sample's `/diff` page walked in Chromium

## 3. The keyboard

- [x] 3.1 Add `Shortcut.FocusScoped` on the web and on Photon, and use it in the editor and the diff
- [x] 3.2 Name the Mac's function keys and PageUp/PageDown, and keep them from the input method
- [x] 3.3 Check: both hosts fail with the scope check taken out, and F7 steps only the diff in use in
      the browser

## 4. The translation

- [x] 4.1 Name each member of an extended property pattern, and answer false on a null on the path
- [x] 4.2 Quote every string through one writer that escapes its line breaks
- [x] 4.3 Fold a negative declared default, and write a record's nullable delegate and decimal right
- [x] 4.4 Check: the conformance suite on both sides, and the runtime's `tsc` over every twin

## 5. Documentation

- [x] 5.1 The wiki's code editor and components pages, in English and Portuguese
- [x] 5.2 The plan's 2b rows and the `docs/LEDGER.md` line citing #420
- [x] 5.3 Archive this change before the merge, so `openspec/specs` on main matches the code
