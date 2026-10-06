# Design

## How Flutter does it

- A drawing at its parent's width: `SvgPicture` takes `width`/`height` like any box, and a parent
  `AspectRatio` or `FittedBox` decides the derived axis. Here the drawing carries its own aspect,
  so `SizeValue.Fill` plus that aspect is the whole of it: the same derivation the dp form already
  makes when the height is omitted.
- A child placed by fraction: Flutter has two widgets, `Align` with a `FractionalOffset` (a point
  in the parent, as fractions) and `FractionalTranslation` (a shift by fractions of the child's own
  size, applied at paint). This repository's rule for a missing word is to look at Flutter first,
  and both concepts are taken. They land on `Positioned` rather than as two new nodes because a
  `Positioned` already IS "a child placed in its stack": a second node that also places a stack
  child would be two doors to one job (#157). Unlike `FractionalTranslation`, the shift moves the
  LAID-OUT position on Photon, so hit-testing follows what is drawn, which is also what a CSS
  `transform` gives the browser's hit-testing.
- A grid that fits as many columns as it can: `SliverGridDelegateWithMaxCrossAxisExtent` divides
  the width into the fewest columns no wider than a maximum. CSS's auto-fill does the inverse, the
  most columns no narrower than a minimum, and the handoffs this SDK answers to write CSS; the
  minimum is the term they use.

`docs/FLUTTER-PARITY.md` gains the three rows.

## Decisions

- **`SizeValue` for the drawing's width, not a second constructor.** The vocabulary already says
  "fill" with `SizeValue.Fill`, `float` converts to it implicitly, and there are no overloads (the
  twin is JavaScript). The old `float` width goes, as preview allows.
- **Fractions as separate properties, not a new value type on the old ones.** `Top` stays the
  point offset every existing caller writes; `TopFraction` adds to it. A tooltip "16 above 30%" is
  `TopFraction = 0.3f, Top = -16`, which CSS spells `calc(30% - 16px)`.
- **The shift is a fraction of the child, the offsets of the stack.** These are the two things a
  handoff asks for (`left: 42%` and `translate(-50%, -100%)`), and the two references differ; one
  property cannot mean both.
- **Auto-fill is the whole column list.** CSS lets `repeat(auto-fill, …)` sit beside fixed tracks,
  and nothing the handoffs draw asks for that; refusing it at construction keeps the native count
  a single division instead of a solver, and the refusal names the way out.
- **Auto-fill on an unbounded width is one track.** There is nothing to divide, and CSS answers
  the same for an intrinsic width.
