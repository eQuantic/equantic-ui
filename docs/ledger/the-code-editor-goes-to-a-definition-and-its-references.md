- **2026-10-26 · The code editor goes to a definition and its references**: the editor reached a line, a
  symbol and back, but not what a name means. F12 goes to where the name at the caret is defined, ⌘F12
  (Ctrl+F12) to where it is implemented and ⇧F12 finds where it is used, from `ICodeDefinitionProvider` (LSP's
  `definition`, `declaration`, `typeDefinition` and `implementation`, one method told which by
  `CodeDefinitionKind`) and `ICodeReferenceProvider` (`references`), as VS Code's `SymbolNavigationAction`
  answers them: one place gone to, near the top of the view and recorded as a jump, several listed in the
  go-to field with their lines, filtered by what is typed, none said in a card at the caret, and the
  references instead on the definition itself. F4 and ⇧F4 walk the places of the last list, shown or not,
  carried through every edit. ⌘ over a name (Ctrl elsewhere) underlines it when a provider says where it is
  defined, the pointer a hand over it, and a click goes there, as VS Code's `ClickLinkGesture` does. A place
  in another document is handed to the app (`OpenRequested`, `CodeEditor.OnOpenRequested`), which owns its
  documents. Under it, a code surface says what the pointer looks like over it (`ICodeSurfaceModel.CursorAt`),
  the hosts hand it the modifiers a pointer passes with and a hover again when they change at rest, and the
  macOS shell's press reads ⌥ and ⌃, which it dropped, so ⌥ and a press now adds a caret in a macOS window
  ([#1084](https://github.com/eQuantic/equantic-ui/issues/1084)). Proposed and archived through OpenSpec
  (`openspec/specs/code-navigation`, `openspec/specs/code-editor`).
