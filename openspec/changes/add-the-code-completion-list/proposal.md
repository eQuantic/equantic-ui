## Why

Closes #297, a sub-issue of #295 (Track I · CodeEditor intelligence). The code engine completes as a
word is typed (#296), and nothing draws it: an editor starts with no provider, so no key goes to a
list nothing shows. This change is the view. `CodeEditor` draws the list at the word it completes, as
every code editor does, and an editable editor completes the language's words and the document's
without anything being asked of the app.

It also closes three defects the list ran into, each a sub-issue of its own: #599, a sub-issue of
#419 (the code editor), where a press under a bounded editor's code landed nowhere; and #631 and
#632, sub-issues of #565 (the transpiler's fences), where eqc wrote an enum member named `Value` as a
read of an object nothing defines, and a shared helper that takes the build context could not
compile in the runtime's own build.

## What Changes

- `CodeEditor` draws the completion list under the word being completed, its labels lined up with
  the word, and over it when the room below runs out. Its rows are the code's own lines: the editor's
  line height and font, the characters the word matched marked, the entry's kind as a letter and its
  detail, and the selected entry's documentation once its provider resolved it. It shows a page of
  rows and keeps the selected one in view as the arrows walk it.
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
  pressable that never takes the keyboard from where it is, which is what a row of the list is, and
  which the declarative `Pressable(…)` factory takes as `canRequestFocus:`.
- A bounded editor's code fills its viewport however short the file (`CodeBlock.MinHeight`): a press
  under the last line puts the caret at the end of the document and gives the editor the keyboard, and
  a short file in a tall pane has the pane's room for a list.
- eqc writes an enum member called `Value` or `HasValue` as its name, like any other; the build context
  a helper class takes is annotated `BuildContext`, the name its module imports; the runtime's context
  carries `typeScale` as the number C# has; and the runtime's `TypeStyle` gains `scaledSize` and
  `scaledLineHeight`, as .NET computes them.

The public surface grows (`CodeEditor.Completions`, `CodeBlock.MinHeight`, the three `CodeSurface`
members, `Pressable.CanRequestFocus`), and two signatures widen, their old shapes gone: `CodeRegion`,
the Photon realizer's record of a code surface, takes where the options landed (`Offered`), and
`UI.Pressable` takes `canRequestFocus`. A caller of either meets a new optional argument and changes
nothing. The developer surface does not move.

## Capabilities

### New Capabilities

- `code-editor`: what a press under a bounded editor's code does.

### Modified Capabilities

- `code-completion`: the list an editor draws, where it stands, what its rows show, what a press on
  one does, what assistive technology is told, and what an editor completes from by default.
- `transpiler-expressions`: an enum member's name, whatever it is, and the build context's annotation.
- `transpiler-vocabulary-values`: a `TypeStyle` scaled by a text-scale factor in the browser.

## Impact

The component library (`CodeEditor`, `CodeBlock`, the list's view `CodeCompletionView`, the `UI`
factories), the vocabulary (`CodeSurface`, `Pressable`), the code engine (`CodeEditorController`), eqc
(`EnumStrategy`, the emitter's context annotation and imports), the web realizer and the runtime's
lowering (the surface's options, the input's listbox attributes, a pressable that keeps the focus where
it is), the runtime's context and `TypeStyle`, the Photon realizer (the options drawn and hit over the
code, their semantics, a press that moves no focus), and their twins. The wiki's code editor and
declarative surface pages, in English and Portuguese, `docs/FLUTTER-PARITY.md`, the plan and the ledger.
