# Tasks

## 1. The measure

- [ ] 1.1 `CodeKeymapBehaviourTests`: every key, every combination of the modifiers, both conventions,
      ten states; the baseline written by the hand-written keymap

## 2. Commands and the table

- [ ] 2.1 `CodeCommand`, `CodeKeyBinding`; `CodeEditorController.Execute`, `CanExecute` and
      `KeyBindings`
- [ ] 2.2 `CodeKeymap.Defaults` for Apple and Standard, and `Handle` dispatching through the table: the
      Tab trap and the chord's wait first, an app's bindings before the defaults, the first command
      that runs claiming the key
- [ ] 2.3 Check: the baseline's diff holds only the loose combinations, every named chord unchanged;
      a custom binding, a rebinding, a freed chord and a command run from code, in the engine's tests;
      the twins executed against .NET

## 3. The component

- [ ] 3.1 `CodeEditor.KeyBindings`, adopted from a parent's next build
- [ ] 3.2 Check: through `PhotonHost` and through the real lowering, a binding of the app's runs

## 4. Documentation

- [ ] 4.1 docs/CODE-EDITOR-PLAN.md: the row of slice 2's first part
- [ ] 4.2 The wiki's CodeEditor page in every language it is written in: the keymap, the commands, an
      app's bindings
- [ ] 4.3 One ledger entry, a file under docs/ledger/, citing #1010
- [ ] 4.4 Archive this change before the merge
