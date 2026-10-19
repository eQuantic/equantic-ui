# Tasks

## 1. eqc and the runtime: Task.Delay

- [x] 1.1 The runtime's cancellable delay (`$eq.task.delay`), reading a `TimeSpan` or milliseconds,
      refusing what .NET refuses with its message, and ending cancelled with the token
- [x] 1.2 eqc writes every `Task.Delay` overload through it
- [x] 1.3 Check: conformance cases executed on both sides (a `TimeSpan`, a token cancelled before and
      during the wait, a delay below -1 for both overloads)

## 2. The engine

- [x] 2.1 Undo and redo raise the edit they made
- [x] 2.2 A range carried through an edit (`CodeRange` through a `CodeEdit`), never growing at its edges
- [x] 2.3 `ICodeDiagnosticProvider` and `CodeDiagnostics`: the app's report, each provider's answer,
      their union in document order, carried through every edit, a document replaced whole dropping
      them; asked after `Delay`, one request out, an answer dropped by its generation, `Check()`,
      `Failed`
- [x] 2.4 `CodeHoverSession`: the pointer's rest, the delay, the word or problem it explains, the
      problems first and the providers' answers after, late answers dropped, the card staying in its
      range, closing on exit, Escape, and a keyboard card closing on a move or an edit
- [x] 2.5 The keymap: F8 and Shift+F8, ⌘K ⌘I, Escape closing a card
- [x] 2.6 The pointer: `Hover` and `Exit` reach the hover session
- [x] 2.7 Check: keystroke and pointer sequences in the engine's tests, and the transpiled twins
      executed by the runtime suite

## 3. The vocabulary and the realizers

- [x] 3.1 `CodeSurface.Notes`
- [x] 3.2 The web: the C# realizer and the runtime's lowering draw the notes over the carets, a pointer
      over a note reaches no model, and the surface reports `Hover` and `Exit`
- [x] 3.3 Photon: the notes laid out and painted over the carets, hit-tested before the code, announced
      in order, and the host reporting `Hover` and `Exit` to a code surface
- [x] 3.4 Check: the vocabulary's contract tests, vitest through the real lowering, `PhotonHost` driven
      with a pointer and keys

## 4. The components

- [x] 4.1 `CodeDecorationKind.Dots`, and `CodeBlock` drawing a squiggle as a wave and dots under a hint
- [x] 4.2 `CodeEditor.Diagnostics`, `DiagnosticProviders` and `HoverProviders`; the problems' squiggles
      and gutter markers; the card (`CodeHoverCard`) placed above or below what it explains, a polite
      live region when the keyboard asked for it
- [x] 4.3 The dashboard sample's code page shows problems and a hover
- [x] 4.4 Check: the component's trees through `PhotonHost`, a running page in a browser, and the
      Photon pixels on the reference backend, light and dark (`code-problems-*` goldens)

## 5. Documentation

- [ ] 5.1 docs/CODE-EDITOR-PLAN.md: where slice 4 stands
- [ ] 5.2 The wiki's CodeEditor page in every language it is written in
- [x] 5.3 One ledger entry, a file under docs/ledger/, citing #299 and #1005
- [ ] 5.4 Archive this change before the merge
