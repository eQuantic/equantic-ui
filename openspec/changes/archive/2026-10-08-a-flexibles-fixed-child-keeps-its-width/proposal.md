# Proposal

Part of #680, a Bug under #166 (Geometry and semantics live in the vocabulary): it answers Copilot's
first round on the pull request, a defect in how Photon sizes a Flexible's child (thread 4220152313)
and a doc that still stated the old floor (thread 4220152423).

## Why

`Slot` pinned the child's main size to the slot as well as the wrapper's. Measured with the web
realizer's own HTML in Chrome 154, and on Photon at 2cbdbb1c9:

- a fixed 400-wide box in a zero weight shrunk to 300: 400 in Chrome, overflowing its item; Photon
  painted it at 300;
- the same box in a weighted share of 300: 400 in Chrome; Photon 300;
- a fixed 100-wide box in a share of 300: 100 in Chrome; Photon stretched it to 300.

And `Flexible.Shrink`'s doc said shrinking "never crosses the min-content floor", where a zero weight
now shrinks past its child's min-content, as the web's `min-width: 0` lets it. The wiki said the same
on two pages.

## What Changes

- On Photon, a Flexible's wrapper takes its slot, and a child that declares its own main size (a fixed
  width, an `Image`, an `Icon`, through transparent wrappers) keeps the size it measured, wider or
  narrower. Any other child still fills the slot: an auto or Fill child measured to it already, and a
  `Text` sizes itself to its lines, so the slot is the line box it fills and aligns them in.
- `Flexible.Shrink` says the rule as it is: shrinking is weighted by the shrink times the size, a
  Flexible's minimum is zero so a zero weight shrinks past its child's min-content, Photon's other
  items keep their floor, and so does a Flexible in a wrapping row for now. WriteOnceComponents and
  Photon say it too, in English and Portuguese.

For a developer: a box with a fixed width inside a Flexible is drawn at its width on Photon, as in a
browser, instead of the Flexible's. Nothing changes on the web.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `flex-layout`: a new requirement, a Flexible's slot sizes the item and a fixed child keeps its own
  size.

## Impact

- Photon: `Layout/MeasureVisitor.Flex.cs` (`Slot`) and a `MainSizeKind` beside `CrossSizeKind`.
- Primitives: `Nodes/Flexible.cs`, a doc comment.
- Not reached: the web realizer and the runtime, eqc, the shells, the SDKs and the templates. The
  public surface and the developer surface do not move.
- Measured and left as they are: the wrapping pass measures a Flexible's child with no wrapper node
  and pins it to the size it resolved, and a centred `Text` in a Flexible starts at the item's edge on
  the web, whose span is inline, where text-align does nothing.
