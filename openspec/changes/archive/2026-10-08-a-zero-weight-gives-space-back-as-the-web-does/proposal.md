# Proposal

Part of #680, a Bug under #166 (Geometry and semantics live in the vocabulary): the pull request's
pre-PR review found the zero weight #680 introduced shrinking on Photon by rules of its own. The doc
half names #728, a Bug filed from this pull request (the wrapped pass's natural size).

## Why

#680 made a zero weight a rigid item on Photon, which an overflowing line can take back. The web
writes it `flex: 0 <shrink> <basis>; min-width: 0`, and Photon took it back by other rules. Measured
with the web realizer's own HTML in Chrome 154, and on Photon at 95f952b2f:

- a zero weight around a 400-wide box, after a rigid 100, in 400: 300 in Chrome, at a basis of 540
  and without one. Photon stopped it at the child's min-content, 400, and the line ran 100 past its
  row;
- `shrink: 3` beside `shrink: 1`, both at 540, in 1000: 480 and 520 in Chrome, where Photon split the
  overflow evenly, 500 and 500; beside a box that fills the row, 206.11 and 793.89, where Photon gave
  473.68 and 526.32;
- a zero weight holding text beside one holding a box, 300 each, in 400: 200 and 200 in Chrome, where
  Photon cut the text first, 100 and 300; two holding text, `shrink: 3` and `shrink: 1`, 136.37 and
  163.63, where Photon gave 180 and 120.

## What Changes

- On Photon, in a single line, a zero weight gives space back as the web's declaration lets it: as
  far as nothing, whatever its child's min-content; by its shrink times its size; and together with
  the other items that shrink, whatever it holds, instead of being cut first as text. A share that
  would carry it below nothing stops there and the rest goes to the others, as CSS resolves it.
- Every other item keeps its rule, its min-content floor and a share in proportion to its room above
  it, so a line without a zero weight resolves to the numbers it always had.
- `FlexNode.Wrap`'s doc states the contract and the two places Photon does not follow it yet: a
  weighted Flexible without a basis breaks at its natural size (#728), and a shrinking Flexible stops
  at its child's min-content. `MeasureFlexWrapped`'s comments say what it does today and name #728.

For a developer: a zero weight in an overflowing row lays out on Photon at the width a browser gives
it. Nothing changes on the web.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `flex-layout`: a new requirement, a zero weight gives space back as the web lets it, and the
  declaration requirement gains the shrink factor's scenario.

## Impact

- Photon: `Layout/MeasureVisitor.Flex.cs`, the single-line pass's text cut, its flex-shrink pass and
  its re-measure of a rigid item.
- Not reached: the web realizer and the runtime, which already wrote the declaration the numbers come
  from; eqc, the shells, the SDKs and the templates. The public surface and the developer surface do
  not move.
- Measured and left as they are, because they are not a zero weight's: an item that is not a zero
  weight still shrinks by its room where CSS shrinks it by its size, a box of words still hugs its
  re-wrapped text after it shrinks, and the wrapped pass still stops a shrinking Flexible at its
  child's min-content.
