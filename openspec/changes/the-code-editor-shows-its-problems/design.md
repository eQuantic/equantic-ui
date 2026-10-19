## Context

docs/CODE-EDITOR-PLAN.md's slice 4 is the editor that explains what it holds: hover, diagnostics that
travel with edits, wavy squiggles and problem navigation (the parity bar's rows for hover and
diagnostics). The engine shipped the records in its first slice (`CodeDiagnostic`,
`CodeDiagnosticSeverity`, `CodeHover`, `ICodeHoverProvider`) and nothing read them. Slice 3 set the
shape this one follows: a session in the engine per interaction (§5), asked once and dropped by its
generation (§6), drawn by the code surface in the code's own coordinates (§7).

## How Flutter solves it

Flutter has no code editor, and three of its pieces answer the parts of this one.

- **Wavy squiggles.** Spell check marks a misspelled word with `TextDecoration.underline` in
  `TextDecorationStyle.wavy` (`SpellCheckConfiguration.misspelledTextStyle`): a decoration of the
  text's own range, in the text's own coordinates. Here the range is a `CodeDecoration` the block
  draws on the cells the engine counts, and the wave is a stroked `Drawing` both realizers already
  paint, so no new vocabulary is needed for it.
- **Hover.** `MouseRegion` hears `PointerHoverEvent` and `PointerExitEvent`, and `Tooltip` waits
  `waitDuration` after a hover before it shows, so a pointer passing over shows nothing. The
  vocabulary already names both phases after Flutter's events (`PointerPhase.Hover`, `Exit`); the
  hosts did not send them to a code surface. The delay is the engine's, because the engine is what
  asks the providers and a provider asked for every passing pointer is a language server flooded.
- **The overlay.** A tooltip is an `OverlayEntry`, painted over everything. The code's cards cannot
  use the vocabulary's `Anchored` (it does not escape a scroller's clip on the web, §7), so they are a
  surface's NOTES, as the completion list is its options: in the surface's coordinates, painted over
  the code and the caret.

docs/FLUTTER-PARITY.md's `GestureDetector` row applies: the hover is a pointer phase the surface
hands its model, not a `Hoverable` around it, because what a hover means (which word, which problem)
is the engine's to decide, as what a press means already is.

VS Code is the parity bar's other column, and it decides the defaults: the hover waits 300 ms
(`editor.hover.delay`), F8 and Shift+F8 walk the markers in document order and wrap, ⌘K ⌘I shows the
hover at the cursor, and a marker keeps its range through edits until its owner reports again.

## Decisions

- **Two sessions, as the completion is one.** `CodeDiagnostics` and `CodeHoverSession` live on the
  controller beside `CodeCompletion`, each driven by keys, text and pointers in a test with no host.
  The hover's session is named for what it is because `CodeHover` is already a provider's answer, as
  LSP's `Hover` is.
- **A problem is data, and so is an answer.** The app reports its set (`Report`), each provider's
  latest answer is a set of its own, and the editor shows their union in document order. A new
  report from either replaces only that source's set.
- **Problems travel with edits.** On every edit a problem's range is carried through it: a position
  before the edit stays, one after it shifts by what the edit inserted and removed, and one inside the
  replaced text goes to the edge of the replacement that keeps the problem covering it. A problem
  never grows at its edges (text typed right before or right after it is not in it). A document
  replaced whole drops every problem, since no range in it means anything in the new one.
- **Undo and redo are edits.** They raised `Changed(null)`, which the event reserves for a document
  replaced whole, so carrying a range through an undo was impossible. They raise the edit they made
  now: one replacement, from the history's own record.
- **Asked as typing pauses, one request out.** An edit restarts the wait (`Delay`, 500 ms); when it
  ends, every provider is asked about the document as it stands; the next edit cancels that request's
  token and its answer is dropped by its generation whether the provider honoured the token or not.
  `Check()` asks at once, which the editor does when its providers are first handed and when a
  document is opened.
- **`Task.Delay` is fixed where it lives.** eqc wrote it as a `setTimeout` of its first argument and
  dropped the token. It writes `$eq.task.delay(delay, token)` now, which reads a `TimeSpan` or
  milliseconds as the cancellation twin already reads a delay, refuses what .NET refuses, and rejects
  with a `TaskCanceledException` when the token is cancelled, at once when it already is.
- **The hover asks after the pointer rests, for the word it rests on.** A `Hover` restarts the wait
  only when it lands outside the word the wait or the card is for. When the wait ends, the problems
  at that position are known at once and the providers are asked; the card shows as soon as anything
  is there to show, problems first, then the providers' answers in the order of `Providers`. A late
  answer is dropped by its generation. The card closes when the pointer leaves the range it explains
  or the surface, on Escape, and, for a card the keyboard asked for, when the caret moves or the text
  changes.
- **A note takes the pointer.** While the pointer is over a note the host reports nothing to the
  model, so a card being read stays open, and a press on a note is the note's.
- **F8 is the problem walk, ⌘K ⌘I the hover.** F8 and Shift+F8 put the caret at the start of the
  next or previous problem, wrapping, and show its card; they are claimed only when there is a
  problem. ⌘K ⌘I is the parity bar's chord: the keymap remembers a pending ⌘K for one key. Slice 2
  makes the keymap data with chords of its own (#601) and takes this one over.
- **Where the card stands.** Above the first line of what it explains, its left edge at the range's
  start, and below the line when the viewport has no room above; inside the viewport sideways, as the
  completion list is. It is as wide as its longest line up to 80 columns and the viewport, and wraps.
- **Severity has a colour and a mark.** Error: the destructive colour; warning: the warning colour;
  information: the info colour; each a wave. A hint is three dots under its first characters, in the
  muted ink (`CodeDecorationKind.Dots`, appended, since the vocabulary's enums are append-only). The
  gutter shows the worst problem of each line as an error or a warning marker behind the app's own
  marker for that line.
- **A screen reader hears the keyboard's card.** A card the keyboard asked for is a polite live
  region; one the pointer asked for is not, since a pointer passing over code is no request to speak.

## Risks and trade-offs

- A provider that never honours its token keeps working after it was cancelled; its answer is still
  dropped. The editor cannot stop work it does not own.
- The card does not take the keyboard (no scrolling or selecting inside it). A long answer is
  clipped to what the viewport holds; VS Code's focusable hover is a later step.
- The wave is a path per problem in view. A file with thousands of problems in view draws thousands
  of paths; the block draws only the rows in its window, which bounds it.
