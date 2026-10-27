## 1. The contracts

- [x] 1.1 `CodeLocation`, `CodeDefinitionKind`, `ICodeDefinitionProvider`, `ICodeReferenceProvider`
- [x] 1.2 `CodeDefinitions` and `CodeReferences` on the controller: every provider asked at once, joined, ordered and deduplicated, a failure reported, a late answer dropped, a request cancelled when the text or the caret moves
- [x] 1.3 `CodeEditor.DefinitionProviders`, `ReferenceProviders` and `OnOpenRequested`; `CodeEditorController.OpenRequested`

## 2. Going to

- [x] 2.1 `RevealNearTopIfOutsideView`, VS Code's gap of a fifth of the view and five lines at least
- [x] 2.2 One answer gone to and recorded as a jump, several listed, none said in a card at the caret; the alternative command on the only definition
- [x] 2.3 A place in another document handed to the app

## 3. The list and the walk

- [x] 3.1 `CodeGoTo` in `Definitions` and `References`: the rows, the nearest place selected, the filter, the preview, accept and close
- [x] 3.2 The places held, carried through edits; F4 and ⇧F4 with the list shown and closed
- [x] 3.3 Keystroke sequences of every scenario in the engine's tests

## 4. The link

- [x] 4.1 The trigger held over a word asks once, shows the link, and every way it goes
- [x] 4.2 A press and its release on the same line go to the definition, the message muted; a drag does not
- [x] 4.3 `ICodeSurfaceModel.CursorAt`, the hand over the link

## 5. The hosts

- [x] 5.1 The web: the pointer's modifiers read as the keys read them, the hover sent again on a modifier key, the cursor from `cursorAt`; vitest through the real lowering
- [x] 5.2 Photon: `PointerMove` with modifiers, the hover sent again when they change, `CursorAt` over code; xUnit through `PhotonHost`
- [x] 5.3 The macOS shell reads the modifiers on moves, on `flagsChanged` and all four on a press; the Windows shell on moves and on the Ctrl key

## 6. Commands, keys and the view

- [x] 6.1 The seven commands in `CodeCommand`, with `CanExecute`; F12, ⌘F12 and Ctrl+F12, ⇧F12, F4 and ⇧F4 in both conventions, and the keymap's behaviour pin
- [x] 6.2 The go-to view draws a place's row and the list's title; the link's underline; the strings in every language the SDK speaks
- [x] 6.3 Component tests on Photon

## 7. The twin

- [x] 7.1 The engine's new types transpile, and the runtime type-checks and passes
- [x] 7.2 The conformance suite runs seeded sessions of definitions, references, the list, the walk and the link on both sides, over Roslyn's answers for a real file (its semantic model's definitions and references)

## 8. Proof and documentation

- [x] 8.1 The dashboard sample's code screen answers definitions and references, driven in a browser: F12, ⇧F12, F4, and ⌘ over a name and a click
- [x] 8.2 The wiki's CodeEditor page in its five languages, docs/CODE-EDITOR-PLAN.md, and a ledger entry citing #1084
- [ ] 8.3 The change archived before the merge
