## Context

A completion entry inserted plain text: `InsertText`, or its label, over the word typed or the provider's
range. Since #1012 the editor holds several carets and runs a command at every one as one undo step, and
since #1068 a range carried through edits can either take in what is typed at its edges (`CarryAnchor`,
`CarryReach`) or never grow there (`Carry`). The editor draws marks from the model on every build
(`CodeDecoration`), on the web and on Photon, from one component.

## Goals / Non-Goals

**Goals:** LSP's snippet syntax read as VS Code reads it; a snippet inserted by a completion entry or by an
app, at every caret; the session's stops, placeholders, mirrors and choices, walked with Tab and ⇧Tab and
ended as VS Code ends it; the line's indentation; the placeholders drawn; a built-in provider of named
snippets.

**Non-Goals:** transformations (`${1/(.*)/${1:/upcase}/}`): parsed, so the text after them is read right,
and inserted as the stop or variable they transform; variables beyond the five an editor can answer
(a file's name, the clipboard, the date), since an editor has no file and a host's clipboard answers late;
a snippet inserted inside a session merged into it, as VS Code merges them: the new one ends the old one.
Each can follow on the same model.

## Decisions

- **How Flutter solves it: it does not.** `EditableText` holds one `TextEditingValue`, and Flutter has no
  snippet engine or tab stops. The model is VS Code's `SnippetParser` and `SnippetSession`, which the
  protocol's syntax was written for, on the carets of #1012.
- **The parser never fails** (`CodeSnippet.Parse`). A construct the grammar does not make is text, from its
  `$` on: `$` alone, `${1`, `${1|a` read as typed. `\` escapes `$`, `}` and `\` in text, and also `,` and
  `|` in a choice. A placeholder's content is parsed as a snippet, so placeholders nest.
- **Expanded at insertion, per caret.** Each copy resolves its variables at its own caret (the text it
  replaces, its line, the word at it), gives a mirror its stop's placeholder text (the first placeholder or
  choice that stop has), and takes its line's indentation: every line after its first starts with the
  indentation of the line it is inserted on, and a tab at a line's start is the language's indent step,
  as VS Code adjusts a snippet's whitespace. An unknown variable becomes a placeholder holding its name, at
  a stop after the snippet's own.
- **Inserted at every caret as one step** (`InsertSnippet`): the command run at every caret inserts the copy
  and leaves its caret at the copy's start, which the placement of #1012 carries through the copies inserted
  before it in the document; each stop is then the copy's start plus where the stop lies in the copy, which
  no other copy's edit reaches into.
- **A stop is every place it is written, in every copy.** Reaching one selects all of them: the primary is
  its first place in the copy at the primary caret, the others are carets, so a mirror is typed with its
  placeholder and a stop of several copies is typed at once.
- **The active stop grows, the others do not**, as VS Code switches a placeholder's stickiness when it
  becomes active: the active stop's ranges take in what is typed at their start or end (`CarryAnchor`,
  `CarryReach`), and every other stop is carried as a mark (`Carry`), so `${1:a}${2:b}` typed at the end of
  `a` lengthens 1 and leaves 2 alone, and an empty `$2` right after it stays after the typing.
- **A placeholder typed over takes its nested stops with it**: when Tab or ⇧Tab leaves a stop whose text is
  not what it was when it was reached, every stop nested in it is dropped, as VS Code drops them.
- **The order of stops** is their numbers ascending, then `$0`, or the end of each copy when the snippet has
  none. Reaching it ends the session with a caret there. ⇧Tab at the first stop is not claimed, so it
  outdents. A snippet whose only stop is `$0`, or with none, inserts and places the caret, with no session.
- **The session ends** at the final stop, on Escape, and when a selection is heard to stand outside every
  stop: a click elsewhere, an arrow out of the placeholder. Escape's order is the list, the card, the panel,
  the snippet, the other carets, then the trap on Tab, as VS Code leaves a snippet before it drops cursors.
- **A choice is a list**: reaching a stop with choices opens the completion's list at it with its options,
  every one shown while the placeholder is selected and filtered by what is typed in it; accepting writes
  the option over every place the stop is written, as one undo step.
- **A completion entry holds a `CodeSnippet`** (`CodeCompletionItem.Snippet`), typed rather than a flag
  beside `InsertText`: a provider parses once, and an LSP adapter maps `insertTextFormat: Snippet` to
  `CodeSnippet.Parse(insertText)`. Accepting replaces around every caret what it replaces around the
  primary, as an entry of text does since #1012.
- **Drawing**: the session answers the placeholders a view's window reaches (`PlaceholdersIn`), and the
  editor washes them under the text in the theme's informational tint, the active one too, as VS Code
  washes every tab stop. An empty stop shows as the caret that stands in it.

## Risks / Trade-offs

- A session holds ranges an app's own edit may cross. → They are carried through every change heard, as a
  problem is, and a selection outside them ends the session.
- Mirrors as carets, rather than text rewritten as the placeholder changes. → It is VS Code's own model,
  and it costs nothing #1012 did not build; a mirror inside another stop's placeholder is typed when that
  placeholder's stop is active, as in VS Code.
- A snippet of thousands of stops. → Placement is linear in the copies, and the view asks for the
  placeholders of its window only.
