## 1. A scroll view goes where it is asked

- [x] 1.1 `ScrollRequest(Version)` with `Offset`, and `ScrollView.Request`, in the vocabulary
- [x] 1.2 The web honours a version once per scroll viewport (`scroll-viewports.ts`), clamped, and reports it
- [x] 1.3 Photon honours it through the scroll clock, clamped, and reports it
- [x] 1.4 Tests on both: a request after the first frame, one already honoured, one past the end; checked in the browser

## 2. The view the controller reveals in

- [x] 2.1 `CodeEditorController.View`, reported by whoever draws the editor, and `Scroll`, the request it asks for
- [x] 2.2 `RevealInCenter` and `RevealInCenterIfOutsideView`, asking nothing before the view is known
- [x] 2.3 `CodeEditor` reports its two scroll views and hands the request to them

## 3. The jump list

- [x] 3.1 `CodeJumpList`: the selection stack's rules, a jump's origin recorded first, fifty entries, walking records nothing
- [x] 3.2 Every entry carried through every edit
- [x] 3.3 `NavigateBack` and `NavigateForward`, centring an entry out of view

## 4. Symbols

- [x] 4.1 `CodeSymbol`, `CodeSymbolKind` (every kind of LSP's `SymbolKind`), `ICodeSymbolProvider`
- [x] 4.2 `CodeSymbols` on the controller: its providers asked in order, a late answer dropped, the answer flattened with containers
- [x] 4.3 `CodeEditor.SymbolProviders`

## 5. The go-to session

- [x] 5.1 `CodeGoTo`: open with a prefix, the query, the entries, the selected one, accept and close
- [x] 5.2 The line, read as VS Code reads it, a visible column through the grid's cells
- [x] 5.3 The symbols, filtered and ranked, the one around the caret selected with nothing typed
- [x] 5.4 The preview centred and washed, the view given back on Escape, a jump recorded on accept
- [x] 5.5 Keystroke sequences of every scenario in the engine's tests

## 6. Commands and keys

- [x] 6.1 `GoToLine`, `GoToSymbol`, `NavigateBack`, `NavigateForward` in `CodeCommand`, with `CanExecute`
- [x] 6.2 The default chords in both conventions; the keymap's behaviour pin, extended to their keys and to a jump made, moved by them alone

## 7. The field and its list

- [x] 7.1 `CodeEditor` draws the field over the top of the code and the list under it, a page that follows the selection
- [x] 7.2 Its keys as focus-scoped shortcuts enabled while it shows; a press on an entry accepts it
- [x] 7.3 The previewed lines washed; the prompts in every language the SDK speaks
- [x] 7.4 Component tests on the web and on Photon

## 8. The twin

- [x] 8.1 The engine's new types transpile, and the runtime type-checks and passes
- [x] 8.2 The conformance suite runs seeded sessions of go-to and of the jump list on both sides
- [x] 8.3 Roslyn's outline of a real file (what its syntax tree declares), flattened and filtered on both sides

## 9. Proof and documentation

- [x] 9.1 The dashboard sample's code screen offers its symbols, and the field is driven in a browser
- [x] 9.2 The wiki's CodeEditor page in its five languages, docs/CODE-EDITOR-PLAN.md, and a ledger entry citing #1083
- [ ] 9.3 The change archived before the merge
