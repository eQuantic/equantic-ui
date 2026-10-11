# Design

## Context

A `Flexible` carries three numbers, the CSS `flex: grow shrink basis` triple. The C# constructor
clamped all three (`Math.Max(1, flex)`, `MathF.Max(0, basis)`, `Math.Max(0, shrink)`), the three
properties are `init`, and the browser's twin clamped nothing. Three readers act on the weight: the
web realizer's `LowerFlexible`, the runtime's `lowerFlexible`, and Photon's flex passes, where
`MeasureVisitor.Flex` already gave a child that is not a Flexible a weight of zero.

## How Flutter solves it

`Flexible.flex`: "If zero, the child is inflexible and determines its own size." `RenderFlex` lays
such a child out first, unbounded on the main axis, and shares what is left among the children
whose flex is positive. CSS says the same with `flex-grow: 0`, and drops a negative `flex-grow`,
`flex-shrink` or `flex-basis` as invalid. Neither answers a zero by raising it to one.

## Decisions

**Zero is honoured, not refused.** It is a meaningful weight in both systems the vocabulary follows,
and Cura wrote it on purpose. Refusing it would make "start at 540 and never grow" inexpressible.

**Without a basis, a zero weight starts from its content.** The web writes `auto` there and keeps
`0%` for a weighted child. Measured in Chrome 154: a 200px child in a `flex: 0 1 0%` item is 0 wide
and in `flex: 0 1 auto` it is 200. Photon measures the item with its main axis decided by its
content, so a Fill inside has nothing to fill, which is what the browser does with a `width: 100%`
inside an `auto` item, and what Flutter refuses outright.

**On Photon a zero weight is a rigid item.** The single-line pass lays it out in pass 1, in a slot of
its basis or from its content, instead of deferring it to the share pass, which skipped it. The slot
is the one a weight's share is laid out in, now one local function for both. It counts in the line's
overflow: it gives space back by its own shrink (`Shrinkable`), and `shrink: 0` keeps it whole,
also against the text cut. It no longer makes a hugging row take the available extent, because a
weight of zero declares no intent to fill.

**A negative number is refused where it is written.** On the C# accessor, as `Text.HeadingLevel`
does, so an object initializer meets the same check as the constructor; in the twin after its
trailing config, as the Text twin does. The basis is also refused when it is not a finite number:
NaN read as no basis and an infinity reached the browser as `Infinitypx`, which it drops. Nothing in
the tree relied on the clamps: every weight a component or a sample computes is floored at 1 by its
own code (`Slider`, `ProgressBar`, the dashboard sample), and no caller passes a basis or a shrink
below zero.

Rejected:

- **Refusing zero at construction.** The issue allows it, and it is the one answer that removes a
  layout both Flutter and CSS can express.
- **Keeping `0%` for a zero weight without a basis.** The server and the browser would agree, on an
  item of width zero, and Photon would have to draw nothing to match.
- **Clamping a negative to zero.** It is the silent clamp again, one value lower.

## Risks

- Photon's single-line pass still ignores the basis of a WEIGHTED child, where CSS starts the item
  from it: `flex: 1 1 540px` beside `flex: 1 1 0%` in 1440 is 990 and 450 in a browser and 720 and
  720 on Photon. It predates this change and does not involve a zero weight. A zero weight in a
  single line agrees with the browser in every case measured, and a wrapping row already resolved
  bases.
- An animated weight that reaches zero changes how its item is sized at the last frame, from a
  share of the leftover to its basis or its content. Both targets do the same, and no component
  animates a weight to zero (`ProgressBar` drops the fill at zero).
