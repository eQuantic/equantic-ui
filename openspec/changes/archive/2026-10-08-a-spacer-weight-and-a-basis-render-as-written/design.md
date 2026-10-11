# Design

## How Flutter solves it

`Spacer({super.key, this.flex = 1}) : assert(flex > 0);` A spacer exists to take a share of the free
space, so a share of nothing is a mistake in the code that wrote it, and Flutter says so where it is
written. The rigid gap is another class there (`SizedBox`), as it is another form here
(`Spacer.Fixed`, `Gap` through the factories).

## Decisions

**A Spacer refuses a weight below 1; a Flexible refuses one below 0.** The two differ because their
zeros mean different things. A Flexible of weight zero still has a child, which keeps its basis or
its size (#680); a spacer has no child, so a spacer with no share is nothing at all. The check sits
on the init accessor, over an explicit backing field, because the rigid form has to hold the zero
the accessor refuses: `Spacer.Fixed` writes the field directly, past the check, and the twin's
`Spacer.fixed` writes it after the constructor has checked. A field-backed accessor (`field`) could
not be written past.

**The basis takes the length rule both sides already share.** `TokenCss.Px` and the twin's `px`
already format every other length the same way, two decimals at most and no trailing zeros, and the
server already wrote the basis through `TokenCss.Px`. The twin's basis was the one length written
raw, so the fix is on that side alone, and it is the shared function rather than a second rule.

## Risks

- `px` and `TokenCss.Px` agree on every basis measured here, and on the bases the fixture pins, but
  not on every float. A float halfway between two hundredths rounds differently on each side:
  `1.005f` is `1.01px` on the server, which formats a float to seven significant digits first, and
  `1px` in the twin, which rounds the double the float holds, `1.00499999523…`. It is the rule every
  length shares, so a width, a padding or a gap has the same edge; it predates this change and is
  out of its scope.
- A Spacer weight below 1 now throws where it used to render a weight of 1. Nothing in the tree
  writes one: ProgressBar adds its counterweight only while the fill is below 1000, and every other
  Spacer is written with the default or a literal.
