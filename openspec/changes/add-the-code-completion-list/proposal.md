## Why

Closes #297, a sub-issue of #295 (Track I · CodeEditor intelligence). The code engine completes as a
word is typed (#296), and nothing draws it: an editor starts with no provider, so no key goes to a
list nothing shows. This change is the view. `CodeEditor` draws the list at the word it completes, as
every code editor does, and an editable editor completes the language's words and the document's
without anything being asked of the app.

## What Changes

- `CodeEditor` draws the completion list under the word being completed, its labels lined up with
  the word, and over it when the room below runs out. Its rows are the code's own lines: the editor's
  line height and font, the characters the word matched marked, the entry's kind and detail, and the
  selected entry's documentation once its provider resolved it. It shows a page of rows and keeps the
  selected one in view as the arrows walk it.
- `CodeEditor.Completions` says what the editor completes from: null, the default, is the language's
  words and the document's (`CodeKeywordCompletionProvider`, `CodeWordCompletionProvider`); an empty
  list turns completion off; an IDE passes its language service, with or without the built-ins. A
  read-only editor completes nothing.
- A press on a row accepts it, and the keyboard stays in the code.
- The list is the code input's listbox: on the web the rows are options, the input points at the
  selected one (`aria-activedescendant`), and says the list is open and which list it is; on Photon the
  rows announce as options, the selected one selected.
- The vocabulary grows two pieces. `CodeSurface.Options`, `OptionsOrigin` and `HighlightedOption`:
  what a surface offers at its caret, drawn in the code's own coordinate space over the code, with the
  option the keyboard is on. `Pressable.CanRequestFocus` (Flutter's `InkWell.canRequestFocus`): a
  pressable that never takes the keyboard from where it is, which is what a row of the list is.

No break. The public surface grows (`CodeEditor.Completions`, the three `CodeSurface` members,
`Pressable.CanRequestFocus`), and the developer surface does not move.

## Capabilities

### New Capabilities

None: the list is the code completion capability's view.

### Modified Capabilities

- `code-completion`: the list an editor draws, where it stands, what its rows show, what a press on
  one does, what assistive technology is told, and what an editor completes from by default.

## Impact

The component library (`CodeEditor`, a private view of the list), the vocabulary (`CodeSurface`,
`Pressable`), the web realizer and the runtime's lowering (the surface's options, the input's
listbox attributes, a pressable that keeps the focus where it is), the Photon realizer (the options
drawn and hit over the code, their semantics, a press that moves no focus), and their twins. The
wiki's code editor page, in English and Portuguese, and `docs/FLUTTER-PARITY.md`.
