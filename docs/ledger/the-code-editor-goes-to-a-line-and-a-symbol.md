- **2026-10-25 · The code editor goes to a line and a symbol, and back**: an editor of thousands of lines
  had no way to a line but scrolling, no list of what the document declares and no way back after a jump.
  ⌃G opens a field over the code that reads a line and a column as VS Code's "Go to Line" reads them, ⇧⌘O
  lists the document's symbols, from `ICodeSymbolProvider` (LSP's `documentSymbol`, typed as `CodeSymbol`
  and `CodeSymbolKind`), filtered by the completion's matcher, the first word against the name and the
  others against the container, and the field's text keeps VS Code's `:` and `@` so one becomes the other.
  While it is typed the code is centred on what it reads and its lines are washed, the caret where it
  was; Enter goes there and Escape gives back the view exactly as it stood. Every move of the caret is
  heard by the jump list, by the rules of VS Code's navigation history, a jump always a place of its own,
  and ⌃- and ⌃⇧- (Alt+← and Alt+→ elsewhere) walk it, its places carried through every edit as Rider's are.
  The field is the field's own listbox, as a `Select`'s is, on both targets from the same C#. Under it, a
  `ScrollView` can be told where to go after its first frame (`ScrollView.Request`, Flutter's `jumpTo`),
  which no component could do before, and the controller knows the view it is drawn in, so it centres a
  position (`RevealInCenter`) and gives a view back. And eqc annotates an empty `[]` in a typed local
  with its item, as it did `new List<T>()`, where TypeScript refused the untyped array
  ([#1086](https://github.com/eQuantic/equantic-ui/issues/1086))
  ([#1083](https://github.com/eQuantic/equantic-ui/issues/1083)). Proposed and archived through OpenSpec
  (`openspec/specs/code-navigation`).
