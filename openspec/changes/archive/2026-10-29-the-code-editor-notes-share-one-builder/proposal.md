# Proposal

Closes #1141, a sub-issue of #295 (Track I · CodeEditor intelligence), and the two transpiler bugs the
extraction met, #1164 and #1169, sub-issues of #849.

## Why

The hover card (`CodeHoverCard`) and the signature panel (`CodeSignatureCard`) each built the same note
over the code: a bordered box padded `Space.S2` across and `Space.S1` down, the prose and code line
heights from the metrics' style, the width of each line from `Widest`, the lines a text wraps to from
`LinesOf`, a width capped at 80 columns and the view and rounded up to a whole pixel, and a place above
or below the line, kept inside the view. The panel copied all of it, and the card's members were widened
to `internal` only so it could. The whole pixel is the step a copy forgets, and a browser's layout then
cuts the widest line with an ellipsis: the next note (the find widget, code actions) would have been the
third copy.

The builder is the first plain class of the shared library whose last step is called `Build`, and eqc
wrote it as a component: the parser and the dependency resolver took a non-private `Build` for a
component's entry point even with the project's compilation in hand, which says the class derives from
nothing but `object` (#1164). Its twin extended `StatelessComponent` and typed `announced` as a
`BuildContext`. Written as the plain class it is, its `TypeStyle` properties started as a bare
`undefined`, which the runtime's strict build refused (#1169): a reference's null is said to be one there
(`null!`), and a struct with no zero of its own was not.

## What Changes

- One owner, `CodeNoteBuilder` (`src/eQuantic.UI.Components`), an internal class in a file of its own:
  the two faces and their line heights, the measuring (`Widest`, `LinesOf`), the width (`Want`, then
  `Fit`, which rounds the longest line up to a whole pixel and caps it), the parts stacked with their
  height (`Add`), the frame (`Build`, or `Frame` round a single part), and where a note stands (`Place`:
  over the line, under it when the view has no room above, and on the side another note leaves free).
- The hover card, its not-found message and the signature panel build through it, and the editor places
  all three through `CodeNoteBuilder.Place`. `CodeHoverCard.Place` and `CodeSignatureCard.Place` are gone,
  and the hover card keeps private what only it uses.
- Nothing a person sees, and nothing a developer using the SDK writes, changes: the arithmetic keeps its
  order, so every width and height is the float it was.
- eqc decides whether a class is a component by its chain of bases wherever the compilation binds it, in
  the parser and in the resolver alike (`TypeSymbolExtensions.BasesBind`); the name of a method decides
  only where a base on the chain does not bind, or the host has no compilation (#1164). The chain names
  `UiComponent`, the root every component derives from, as a kind of its own, so a component written over
  the root itself stays one (found by Copilot's review of the pull request).
- A plain class's member that TypeScript reads as never null and starts as `undefined` is said to be one,
  `undefined!`, as one that starts as null already was (`TwinConstructor.StartOf`, #1169).

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `transpiler-classes`: a class is a component by what it derives from, and a member TypeScript reads as
  never null starts as a value TypeScript takes. The notes' extraction itself moves no requirement.

## Impact

- **Code**: `src/eQuantic.UI.Components` (`CodeNoteBuilder`, `CodeHoverCard`, `CodeSignatureCard`,
  `CodeEditor`) and their transpiled twins under `src/eQuantic.UI.Runtime/src/shared/components/`.
- **Compiler**: `ComponentParser`, `ComponentDependencyResolver`, `TypeSymbolExtensions` and
  `TwinConstructor`, with `ComponentByItsBasesTests` and a case in `EmittedTypeSurfaceTests`.
  `RuntimeProvidedClassTests` wrote its probe as `class Probe { public int Build() … }`, a component only
  by that name: it now says `: StatelessComponent`, since the fallback it pins is the component path's.
  Where no compilation exists, a plain class still imports no `[RuntimeProvided]` helper, and the parser and
  the resolver still read different names where a base does not bind: #1170, measured on main.
- **The builder's order**: `Want`, `Fit`, then `Add` and `Build`; out of that order it throws, since a note
  built before its width is settled would be drawn nothing wide. The not-found card's frame is pinned in
  `CodeDefinitionsViewTests` (a box round the text itself, a whole pixel wide), which had no golden.
- **Public surface**: none. The types are internal, and the developer surface does not move. An app's
  class over no component's base that declares `Build` is now written as the plain class it is.
