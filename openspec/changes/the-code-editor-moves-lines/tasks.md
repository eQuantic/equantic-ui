# Tasks

## 1. The edit door

- [x] 1.1 `Edit` takes the selection an edit leaves and records it as the edit's selection after, so redo
      puts it back

## 2. The operations

- [x] 2.1 `CodeEditorController.MoveLines`, `CopyLines`, `DeleteLines`, `JoinLines`, `SortLines` and
      `InsertLine`, each one replacement over the lines the selection takes
- [x] 2.2 The ten commands, appended to `CodeCommand`, their `Execute` and `CanExecute`
- [x] 2.3 The keys in both default tables, ⌥↑ ⌥↓ after the panel's
- [x] 2.4 Check: command sequences in the engine's tests for every operation, its edges, undo and redo;
      the keymap's behaviour baseline regenerated and its diff read; the twins executed against .NET

## 3. Documentation

- [ ] 3.1 docs/CODE-EDITOR-PLAN.md: the row of slice 2's second part
- [x] 3.2 The wiki's CodeEditor page in every language it is written in
- [x] 3.3 One ledger entry, a file under docs/ledger/, citing #1011
- [ ] 3.4 Archive this change before the merge
