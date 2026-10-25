## Why

Closes #1013, the fourth part of slice 2 of docs/CODE-EDITOR-PLAN.md (#601), on the multiple carets of #1012
and the completion of slice 3. The parity bar's row "Snippets with tab stops, placeholders, choices,
mirrors" is open on both targets: a completion could insert only plain text, so a provider offering
`for (var i = 0; i < count; i++)` left the person to select `i` and `count` by hand, and the plan's
built-in providers were waiting for "slice 2's snippet engine" to offer snippets at all.

## What Changes

For a developer using the SDK, and for the person typing in the editor it gives them:

- `CodeSnippet.Parse(body)` reads the Language Server Protocol's snippet syntax, which VS Code's snippets
  and every language server's completion items are written in: `$1` and `${1}` a tab stop, `${1:name}` a
  placeholder (placeholders nest), `${1|one,two|}` a choice, `$0` the final stop, a stop written twice a
  mirror, and the variables `$TM_SELECTED_TEXT`, `$TM_CURRENT_LINE`, `$TM_CURRENT_WORD`, `$TM_LINE_INDEX`
  and `$TM_LINE_NUMBER`, with `${NAME:default}`. An unknown variable inserts its name as a placeholder, as
  the protocol says. What the syntax does not make is text, as VS Code reads it.
- `CodeCompletionItem.Snippet`: an entry that inserts a snippet, at every caret, as one undo step.
  `CodeEditorController.InsertSnippet(snippet)` inserts one in place of every selection, for an app's own
  command (a toolbar's "insert a loop").
- `CodeSnippetCompletionProvider` offers the snippets an app lists, each a `CodeSnippetDefinition` with the
  prefix typed to find it, its body and a description: the built-in provider slice 3 left for this one.
- Once inserted, the snippet is a session (`CodeEditorController.Snippet`): its first stop is selected; Tab
  goes to the next stop and ⇧Tab to the one before; a placeholder is selected when its stop is reached; a
  mirror is a caret at every place its stop is written, so it is typed with it (#1012's carets); a choice
  opens its options as a list at the stop; reaching `$0`, or the snippet's end when it has none, ends the
  session, and so do Escape and a caret that leaves the snippet. A stop nested in a placeholder that was
  typed over is gone.
- A snippet takes the line's indentation: every line after its first starts with the indentation of the
  line it is inserted on, and a tab at a line's start in its body is the language's indent step.
- Every placeholder of the snippet is washed while the session lasts, on the web and on Photon, from the
  same C#.
- Escape's order is the list, the card, the panel, the snippet, the other carets, then the trap on Tab.
- `CodeCompletionItem.Replacing` and the accept requirement still said a provider's range keeps its
  distance from the end of the line, which #1068 replaced with ranges carried through every edit: both say
  what the code does.
- And three gaps of eqc the engine met, each closed here. A member holding a `StringBuilder` was annotated
  with a name its module never imported (#1074): the builder is imported where the emitted text names it,
  as the runtime's other .NET-compat types are. A member holding a function whose result may be null, a
  `Func<string, string?>`, started as a bare `null` the type check refused (#1075): only a union at the top
  level makes a member nullable, so the `| null` after a function's arrow is its result's. And a list's own
  `Reverse()` was LINQ's, a reversed copy thrown away, with `List<T>.Reverse(index, count)` and
  `Array.Reverse(array, index, length)` reading no range (#1076): they reverse in place, their arguments
  checked as .NET checks them, which the conformance suite of the snippets found when every copy's stops
  landed at another caret on the web.

The parts it reaches: eqc (`TwinConstructor`, `TypeScriptEmitter`'s list of the runtime's .NET-compat
types, and the list, array and LINQ strategies of `Reverse`), the runtime (`runtime-exports`, and
`utils/list.ts`'s `listReverse` and `arrayReverse`), the engine (`eQuantic.UI.Code`: `CodeSnippet`, its parser and session, the
controller's insertion, the keymap's bindings and the commands appended to `CodeCommand`, the completion's
choices and snippet entries), the `CodeEditor` component, which washes the placeholders, and through the
twins both realizers. The public surface grows and nothing leaves it; `CodeCommand` gains three values at
its end. No migration line.

## Capabilities

### New Capabilities

- `code-snippets`: the snippet syntax, inserting a snippet, the session's stops, mirrors and choices, how it
  ends, its indentation, the built-in provider, and the placeholders drawn.

### Modified Capabilities

- `code-completion`: accepting an entry that holds a snippet inserts it, and the provider's range is the
  one carried through every edit.
- `code-cursors`: Escape leaves a snippet before it takes away the other carets.
