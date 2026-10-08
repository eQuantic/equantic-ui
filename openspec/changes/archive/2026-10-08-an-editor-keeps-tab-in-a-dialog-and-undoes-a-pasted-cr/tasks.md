# Tasks

## 1. Tab inside a dialog (#598)

- [x] 1.1 The focus trap hears Tab in the bubble phase and cycles only a Tab nothing prevented
- [x] 1.2 Check: the real code surface, lowered and mounted at either end of a marked layer, indents on
      Tab, outdents on Shift+Tab, and moves on after Escape, failing on the trap before the fix

## 2. Undo after a paste (#600)

- [x] 2.1 An edit records the text the document holds, and its range breaks lines as the document does
- [x] 2.2 The engine's twins regenerated, and the runtime's suite run on them
- [x] 2.3 Check: undo and redo after pasting a lone CR, a CRLF, a LF and a mix, the edit a listener
      hears, and the range of an edit built by hand, failing on the engine before the fix
- [x] 2.4 The history ends a run of typing at a CR as at a LF, checked with a typed lone CR recorded by
      hand, failing on the history before the fix

## 3. Documentation

- [x] 3.1 The wiki's CodeEditor page, in English and Portuguese
- [x] 3.2 One `docs/LEDGER.md` line citing both issues
- [x] 3.3 Archive this change before the merge
