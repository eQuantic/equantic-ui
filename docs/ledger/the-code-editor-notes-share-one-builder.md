- **2026-10-29 · The code editor's notes are built by one builder, and a builder is no component**: the
  hover card and the signature panel each built the same note over the code, and the panel copied the
  card's frame, measuring and placement, whose members had been widened to `internal` only so it could.
  `CodeNoteBuilder` owns them now: the two faces and their line heights, the width of the longest line
  rounded up to a whole pixel and capped at 80 columns and the view (the pixel a copy forgets, and a
  browser's layout then cuts the line with an ellipsis), the parts stacked with their height, the frame,
  and where a note stands over the line, on the side another note leaves free. The card, the not-found
  message and the panel build through it, and a third note (the find widget, code actions) will too.
  Nothing a person sees changed: the goldens `code-problems-*` and `code-signature-*` match byte for byte
  ([#1141](https://github.com/eQuantic/equantic-ui/issues/1141)). The builder met two transpiler bugs on
  the way. eqc took a class declaring `Build` for a component even where the compilation said it derived
  from nothing but `object`, and wrote its twin over `StatelessComponent`; the parser and the dependency
  resolver now ask the chain of bases wherever the compilation binds it, and a method's name only where a
  base does not bind ([#1164](https://github.com/eQuantic/equantic-ui/issues/1164)). And a plain class's
  member of a struct with no zero of its own started as a bare `undefined`, which the runtime's strict
  build refused; it is said to be one, `undefined!`, as a reference's `null!` already was
  ([#1169](https://github.com/eQuantic/equantic-ui/issues/1169)). Proposed and archived through OpenSpec
  (`openspec/specs/transpiler-classes`).
