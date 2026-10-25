# Tasks

## 1. The syntax

- [x] 1.1 `CodeSnippet.Parse`: tab stops, placeholders that nest, choices, `$0`, variables with defaults,
      transformations parsed and dropped, escapes; anything else text
- [x] 1.2 The expansion of a copy at a caret: mirrors given their stop's text, variables resolved there, an
      unknown variable a placeholder after the snippet's stops, the line's indentation and the indent step
- [x] 1.3 Check: the engine's parser tests, every construct and every malformed one read as text

## 2. The session

- [x] 2.1 `InsertSnippet` at every caret as one step, each copy placed through the ones before it; the first
      stop selected, or a caret at `$0` or the end with no session
- [x] 2.2 The stops: every place in every copy, the active one growing at its edges and the others carried
      as marks; Tab, ⇧Tab, the final stop; nested stops of a placeholder typed over dropped
- [x] 2.3 The end: the last stop, Escape (`LeaveSnippet`), a selection outside every stop
- [x] 2.4 The keys: Tab and ⇧Tab after the list's and before indenting, Escape before the other carets, in
      both default tables; `CodeCommand` gains `NextTabStop`, `PreviousTabStop`, `LeaveSnippet` at its end
- [x] 2.5 Check: the engine's keystroke sequences for every scenario of `code-snippets`, and the keymap's
      behaviour baseline regenerated and its diff read

## 3. Completion and choices

- [x] 3.1 `CodeCompletionItem.Snippet`: accepting inserts it around every caret
- [x] 3.2 A choice: the list at the stop with its options, all of them while the placeholder is selected;
      accepting writes the option over every place of the stop
- [x] 3.3 `CodeSnippetCompletionProvider` and `CodeSnippetDefinition`
- [x] 3.4 `Replacing`'s documentation and the accept requirement say the range is carried
- [x] 3.5 Check: the engine's completion tests for a snippet entry, a choice, and the provider

## 4. Drawing and both targets

- [x] 4.1 `PlaceholdersIn` and the editor's wash over every placeholder in its window
- [x] 4.2 The twins regenerated; the conformance suite runs snippet sessions on .NET and in the browser and
      compares them
- [x] 4.3 Check: the runtime's tests, the conformance suite, and a snippet inserted, walked and chosen in
      the dashboard's editor in a browser

## 5. Documentation

- [x] 5.1 The wiki's CodeEditor page in all five languages: snippets, the keys, the provider
- [x] 5.2 docs/CODE-EDITOR-PLAN.md: the parity row and slice 2
- [x] 5.3 The ledger's entry, a file of its own under docs/ledger/, citing #1013
- [x] 5.4 Check: the docs guards against the wiki branch, and `openspec validate --all --strict`
