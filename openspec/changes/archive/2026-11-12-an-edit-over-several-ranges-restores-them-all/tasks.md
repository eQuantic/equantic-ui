# Tasks

## 1. The fix

- [x] 1.1 `SheetSelection`: every range, the active one last, and the active cell
- [x] 1.2 Every edit records the whole selection before and after it, and undo and redo restore it
- [x] 1.3 A paste puts the active cell where it landed

## 2. Proof

- [x] 2.1 Delete and Ctrl+Enter over several ranges: undo and redo give every range back with the active cell
- [x] 2.2 An active cell Tab walked inside its range comes back where it was
- [x] 2.3 The twin answers the same: the ranges session of the conformance suite gains a redo

## 3. Documentation

- [x] 3.1 The spreadsheet spec, the ledger and the wiki's Upgrading page
