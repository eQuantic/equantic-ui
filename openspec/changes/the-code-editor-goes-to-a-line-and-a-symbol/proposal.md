## Why

Closes #1083, the first part of slice 5 of docs/CODE-EDITOR-PLAN.md (#602, under the code editor's epic
#419). An editor with thousands of lines has no way to reach a line but scrolling, no outline of what the
document declares, and no way back to where the person was before a jump: three things every IDE's editor
has, and the parity bar's navigation rows are open on both targets. Slice 5's definitions and references
(#1084) record their jumps in the same list, so the list comes first.

## What Changes

For a developer using the SDK, and for the person in the editor it gives them:

- ⌃G (Ctrl+G on Windows and Linux) opens a field over the code that takes a line, and a column after a
  colon, a comma or a hash, as VS Code's "Go to Line" reads it: a negative number counts from the end, a
  number past the end is the last line, and the column is a visible column, a tab taking the cells to its
  stop. While it is typed the code follows it, centred on the line, which is washed, and the selection stays
  where it was. Enter goes there, Escape gives back the view exactly as it was.
- ⇧⌘O (Ctrl+Shift+O) opens the same field on the document's symbols: the classes, methods, properties and
  the rest a language service declares, filtered as it is typed (the first word against the name, the
  others against the container), the code following the selected entry. With nothing typed, the symbol the
  caret stands in is selected. The field's text starts with `:` for a line and `@` for a symbol, as VS
  Code's does, so one becomes the other by its first character.
- `ICodeSymbolProvider` answers a document's symbols, shaped after LSP's `textDocument/documentSymbol`:
  a `CodeSymbol` has its name, its kind (`CodeSymbolKind`, every kind of LSP's `SymbolKind`), its range and
  the range that names it, a detail, whether it is deprecated, and the symbols it holds. An IDE adds its
  language service to `CodeEditorController.Symbols.Providers`, and `CodeEditor.SymbolProviders` takes them
  as `HoverProviders` and `SignatureProviders` take theirs.
- Every move of the caret is heard by the jump list, as VS Code's navigation history hears it: a move of ten
  lines or more is a new place, a smaller one replaces the place it moved from, and a jump (a line, a
  symbol) is always one. ⌃- and ⌃⇧- (Alt+← and Alt+→ on Windows and Linux) walk back and forward, the code
  centred on the place when it is out of view. The places are carried through every edit, so a line
  inserted above one does not send the person to the line that was there.
- `CodeEditorController.RevealInCenter(position)` and `RevealInCenterIfOutsideView(position)` bring a
  position to the middle of the view, as Monaco's methods of the same names do, for an app's own command.
- The commands are `GoToLine`, `GoToSymbol`, `NavigateBack` and `NavigateForward`, run by an app's menu
  through `Execute` like any other.
- While the field shows, what still reaches the code, typed or pasted before the field takes the keyboard,
  is the field's: ⌃G and a line number typed at once go to the line instead of editing the code.
- A field that anchors a listbox is the combobox in its text control: `aria-activedescendant` rides the
  focused `input`, where a screen reader follows it, and no longer the field's shell, on both web producers.
  And an autofocused `TextEntry` puts its caret at the end of its text on the web, as Flutter and Photon do,
  so the field opened holding `:` takes what is typed after it.
- eqc annotates an empty `[]` in a typed local with its item, as it annotated `new List<T>()` (#1086): the
  session's filter met TypeScript refusing the untyped array.
- A `ScrollView` can be told where to go after it is first drawn: `ScrollView.Request`, a
  `ScrollRequest` with an offset, honoured once per version, as Flutter's `ScrollController.jumpTo` and the
  table's `TableScrollRequest` are. Until now its `Offset` was read on its first frame alone, so no
  component could scroll its own view again.

The parts it reaches: eqc (`LocalDeclarationStrategy`), the vocabulary (`ScrollView.Request`,
`ScrollRequest`), both realizers (the web's scroll viewports, `Anchored` and `TextEntry` lowering on both web
producers, and Photon's scroll state), the engine (`eQuantic.UI.Code`: the go-to session, the jump
list, the symbol contract, the view the controller reveals in, the commands and the keymap), the
components (`CodeEditor` draws the field and its list, washes the lines it shows and scrolls its view), the
runtime's transpiled twins, and the interface strings in every language the SDK speaks. The public surface
grows in Primitives, Code and Components; nothing goes, and the developer surface is unchanged.

## Capabilities

### New Capabilities

- `code-navigation`: going to a line and to a symbol, the jump list, and revealing a position in the middle
  of the view.

- `anchored-panels`: the combobox of an anchored listbox is the element the keyboard is in.
- `text-entry`: an autofocused entry puts its caret at the end.

### Modified Capabilities

- `scrolling`: a scroll view goes where the app asks it, once per request.

## Impact

- `src/eQuantic.UI.Primitives`: `ScrollRequest`, `ScrollView.Request`.
- `src/eQuantic.UI.Runtime/src/shared` (`lowering.ts`, `scroll-viewports.ts`) and
  `src/eQuantic.UI.Native.Components` (`EmitVisitor.Containers.cs`, the scroll clock).
- `src/eQuantic.UI.Code`: `Navigation/` (the go-to session, its entries, the jump list), `Intelligence/`
  (the symbol contract), `CodeEditorController`, `CodeCommand`, `CodeKeymap`.
- `src/eQuantic.UI.Components`: `CodeEditor`, a `CodeGoToView` beside the completion's, `SdkStrings`.
- The keymap's behaviour pin moves by the new chords alone.
- The wiki's CodeEditor page in its five languages, docs/CODE-EDITOR-PLAN.md, the ledger.
