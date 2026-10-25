- **2026-10-25 · The code editor inserts snippets**: a completion could insert only plain text, so an entry
  offering a loop left the person to select its variable and its bound by hand. An entry inserts a snippet
  now, written in the Language Server Protocol's syntax and read as VS Code reads it, never failing:
  `CodeSnippet.Parse`, then `CodeCompletionItem.Snippet`, `InsertSnippet` for an app's own command, and
  `CodeSnippetCompletionProvider` for the snippets an app lists by prefix. A copy goes in at every caret as
  one undo step, taking its line's indentation, and the session walks its stops with Tab and ⇧Tab, every
  place a stop is written selected with it, so a mirror is a caret and is typed with its stop. The stop the
  carets stand at grows at its edges while the others are carried as marks, as VS Code switches a
  placeholder's stickiness; a placeholder typed over takes its nested stops; a choice shows as the
  completion's list; the final stop, Escape, between the panel and the other carets, and a caret that
  leaves end it, and its placeholders are washed on both targets. Three gaps of eqc the engine met are
  closed with it: a `StringBuilder` member is imported where it is named
  ([#1074](https://github.com/eQuantic/equantic-ui/issues/1074)), a function member whose result may be null
  starts as the null a member takes ([#1075](https://github.com/eQuantic/equantic-ui/issues/1075)), and a
  list's own `Reverse` reverses it in place, a range of a list or an array too, where it was LINQ's copy
  thrown away ([#1076](https://github.com/eQuantic/equantic-ui/issues/1076))
  ([#1013](https://github.com/eQuantic/equantic-ui/issues/1013)). Proposed and archived through OpenSpec
  (`openspec/specs/code-snippets`).
