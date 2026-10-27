## Why

Closes #1084, the second and last part of slice 5 of docs/CODE-EDITOR-PLAN.md (#602, under the code editor's
epic #419). The editor reaches a line, a symbol and back (#1083), but not what a name in the code means: no
key goes to where a method is declared, no click follows a name to it, and nothing lists where a symbol is
used. Those are the parity bar's two open navigation rows, on both targets, and the reason an IDE's editor is
more than a text field with colours.

## What Changes

For a developer using the SDK, and for the person in the editor it gives them:

- `ICodeDefinitionProvider` answers where what is at a position is defined, shaped after LSP's
  `textDocument/definition`, `declaration`, `typeDefinition` and `implementation`: one method, told which of
  the four is asked (`CodeDefinitionKind`), answering `CodeLocation`s. A location is a range and the
  document it is in, null for the document the editor shows. `ICodeReferenceProvider` answers where it is
  used, after `textDocument/references`, the declaration included as VS Code asks. An IDE adds its language
  service to `CodeEditorController.Definitions.Providers` and `References.Providers`, and `CodeEditor` takes
  them as `DefinitionProviders` and `ReferenceProviders`, as it takes `SymbolProviders`.
- F12 goes to the definition of what is at the caret, ⌘F12 (Ctrl+F12 on Windows and Linux) to its
  implementations, and `GoToDeclaration` and `GoToTypeDefinition` are commands an app's menu runs, as VS
  Code binds no key to them. One answer is gone to: the caret at its start, the line brought near the top of
  the view when it is out of it, and the jump recorded in the jump list. Several are listed. None is said in
  a card at the caret, "No definition found for 'amount'", as VS Code says it. F12 on the definition itself
  lists its references, VS Code's alternative command.
- ⇧F12 finds the references of what is at the caret: one is gone to, several are listed, none is said.
- The list is the go-to field's (#1083), over the code: a row for each place, the text of its line with the
  name marked and where it stands, the place nearest the caret selected first, the code centred on the
  selected place and its lines washed. What is typed in the field filters the rows by their text. Enter goes
  to the selected place, Escape gives back the view exactly.
- F4 and ⇧F4 walk the places of the last list, the next and the one before, whether the list still shows or
  not: each is a jump, carried through every edit as the jump list's places are, so a person fixing every
  use of a name in turn keeps walking the right places.
- Holding ⌘ (Ctrl on Windows and Linux) over a name underlines it when a provider says where it is defined,
  and the pointer becomes a hand over it; a click there goes to the definition. Pressing the key with the
  pointer at rest shows the link, and releasing it, another key, a drag, an edit or a scroll takes it away.
- A place in another document is handed to the app, which owns its documents:
  `CodeEditorController.OpenRequested` and `CodeEditor.OnOpenRequested` with the `CodeLocation`, so the app
  opens the file and puts the caret there.
- `CodeEditorController.RevealNearTopIfOutsideView(position)` brings a position near the top of the view, a
  fifth of the view above it and five lines at least, as VS Code reveals what it goes to.
- The commands are `GoToDefinition`, `GoToDeclaration`, `GoToTypeDefinition`, `GoToImplementation`,
  `FindReferences`, `NextReference` and `PreviousReference`, run by an app's menu through `Execute`.

Under them, for every code surface:

- A code surface says what the pointer looks like over it, `ICodeSurfaceModel.CursorAt`, as a sheet surface
  already does: the text cursor, and the hand over a link. Both realizers ask it as the pointer moves.
- The hosts report the modifiers a pointer passes with, and a hover again when the modifiers change with the
  pointer at rest: on the web, with the modifiers read as the keys read them (⌘ and ⌃ apart on a Mac), and
  on Photon, whose macOS and Windows shells now report the modifiers on every move, and the ⌥ and ⌃ a press
  carries on macOS, which they dropped: a ⌥ and a press added no caret in a macOS window (#1012's gesture).

The parts it reaches: the vocabulary (`ICodeSurfaceModel.CursorAt`), the engine (`eQuantic.UI.Code`: the
contracts, the definitions and the references on the controller, the list in the go-to session, the walk,
the link, the commands and the keymap), the components (`CodeEditor` and the go-to view), both realizers
(the web's code surface lowering, `PhotonHost`, and the macOS and Windows shells), the runtime's transpiled
twins, and the interface strings in every language the SDK speaks. The public surface grows in Primitives,
Code, Components and the native track. One member changes shape: `CodeGoToEntry.Kind` is a nullable
`CodeSymbolKind`, null for a place, and `ICodeSurfaceModel` gains a member every implementation writes; an
app that only uses `CodeEditor` meets neither. The developer surface is unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `code-navigation`: definitions, references, the list of places, the walk, the link and opening a place in
  another document.
- `code-editor`: the hosts report the modifiers with the pointer, and a code surface says what the pointer
  looks like over it.

## Impact

- `src/eQuantic.UI.Primitives/Nodes/ICodeSurfaceModel.cs`
- `src/eQuantic.UI.Code/` (Intelligence, Navigation, Editing)
- `src/eQuantic.UI.Components/` (`CodeEditor`, `CodeGoToView`, `CodeHoverCard`, the strings)
- `src/eQuantic.UI.Runtime/src/shared/lowering.ts` and the transpiled twins
- `src/eQuantic.UI.Native.Components/PhotonHost.cs`, `src/eQuantic.UI.Native.Shell.MacOS/`,
  `src/eQuantic.UI.Native.Shell.Windows/`
- Tests in the engine, the components on Photon, the runtime and the conformance suite; the dashboard
  sample's code screen; the wiki's CodeEditor page in five languages.
