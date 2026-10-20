## Why

Closes #299 and #1005, both sub-issues of #295 (Track I · CodeEditor intelligence), slice 4 of
docs/CODE-EDITOR-PLAN.md. The engine has carried `CodeDiagnostic` and `ICodeHoverProvider` since its
first slice, and nothing used either: a problem an IDE knew about could only be drawn as a straight
red rule the app placed itself, which stayed where it was put as the text around it moved, and no
editor explained anything under the pointer.

## What Changes

For a developer using the SDK:

- `CodeEditor.Diagnostics` takes the problems the app knows about, a build's output for instance, and
  `CodeEditor.DiagnosticProviders` the sources the editor asks itself as the text changes: once typing
  pauses (`CodeDiagnostics.Delay`), with one request out at a time, the one before it cancelled.
  Every problem travels with the edits after it was reported, undo and redo included, until whoever
  reported it reports again.
- The editor draws each problem under its range as a wavy line in its severity's colour (an error,
  a warning, an information) and a hint as three dots under its start, and marks its line in the
  gutter beside the app's own markers.
- `CodeEditor.HoverProviders` says what explains the code under the pointer (`ICodeHoverProvider`).
  Resting the pointer on the code shows a card over the word: the problems reported there first,
  then what the providers answer. F8 and Shift+F8 go to the next and the previous problem and show its
  message; ⌘K ⌘I (Ctrl+K Ctrl+I) shows the card at the caret; Escape closes it. A screen reader hears
  a card the keyboard asked for.
- The controller gains the two sessions that do this, `CodeEditorController.Diagnostics`
  (`CodeDiagnostics`) and `CodeEditorController.Hover` (`CodeHoverSession`), driven by keys and
  pointers with no screen, as the completion is.
- Undo and redo raise `CodeEditorController.Changed` with the edit they made, where they raised it
  with null, which the event reserves for a document replaced whole: a language server listening for
  edits had to read the whole file again after every undo.
- `Task.Delay` waits as .NET waits in the browser: for a `TimeSpan` as for milliseconds, and cancelled
  by its token. It ignored the token and read a `TimeSpan` as no delay at all.

The parts it reaches: the engine (`eQuantic.UI.Code`), the vocabulary (`CodeSurface.Notes`, the
positioned nodes a surface draws over its code and caret, each at its start and top), the components (`CodeEditor`, `CodeBlock`, the new
`CodeHoverCard`), both realizers (the web's C# lowering and the runtime's twin; Photon's surface, its
hit-testing and its semantics), the host input paths (a pointer passing over a code surface and
leaving it now reach the model as `PointerPhase.Hover` and `PointerPhase.Exit`), eqc (`Task.Delay`)
and the runtime (a cancellable delay). The public surface grows; the developer surface does not move.

A break, allowed in preview: `CodeDecorationKind` gains `Dots`, appended, and `CodeEditorController`'s
`Changed` carries an edit after an undo or a redo. A listener that resynchronised on null keeps
working, since an edit is what every other change already carried; nothing to migrate. On the native
track, which 1.0 does not promise yet, `CodeRegion` (a code surface in a Photon frame) gains `Notes`, so
its constructor and its `Deconstruct` widen by one trailing member: a caller that builds one with five
members still compiles, and one that deconstructs it names the sixth.

## Capabilities

### New Capabilities

- `code-diagnostics`: the problems an editor shows, how they travel with edits, how they are asked
  for as the text changes, how they are drawn and how the keyboard walks them.
- `code-hover`: the card that explains the code under the pointer or the caret, when it shows, what
  it holds, where it stands and how it closes.

### Modified Capabilities

- `code-editor`: undo and redo raise the edit they made; a code surface draws notes over its code and
  caret; both hosts report a pointer passing over a code surface and leaving it.
- `transpiler-bcl`: `Task.Delay` in the browser.
