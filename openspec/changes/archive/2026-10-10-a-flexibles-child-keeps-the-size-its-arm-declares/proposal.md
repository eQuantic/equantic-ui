# Proposal

Part of #680, a Bug under #166 (Geometry and semantics live in the vocabulary): found when this pull
request was merged, in a rehearsal, with #739, which lays an AdaptiveNode's arm out in the node's
place on Photon.

## Why

Measured with the web realizer's own HTML in Chrome 154, and on Photon at f57db061e:

- In a share of 300, Chrome keeps an AdaptiveNode's box arm of 400 at 400 and one of 100 at 100, as
  it keeps the bare boxes. Photon drew both at 300: the classifier that decides whether a Flexible's
  child keeps its size asked the AdaptiveNode as written, which declares no size of its own.
- In the same share, Chrome caps a scroller arm 400 wide around 800 of content at 300 and scrolls it
  by 500, and leaves a scroller arm of 100 at 100, scrolling by 700. Photon drew both at 300, and the
  capped one scrolled by 400: nothing told the arm its width was a ceiling.
- In a wrapping row of 300, Chrome caps `Flexible(ScrollView { Width = 400 }, flex: 1)` around 800 of
  content at 300 and scrolls it by 500, with a basis of 300 or of 200. Photon drew it at 300 and
  scrolled it by 400: the wrapping pass caps a scroller when it measures it again, and the scroll
  range a measure registers was the first one's.

## What Changes

- The size classifiers are asked of the node a child MEASURED to (`SizedBy`), walking the measured
  tree through what takes its one child's size, rather than of the child as written. An AdaptiveNode
  is never in that tree, since it measures to its arm, so it needs no answer of its own.
- The slot measures the child, and measures it again under the ceiling when what it measured to is a
  scroller wider than the item; the wrapping pass asks the same of its first measure.
- A scroller registers the scroll range of the measure the tree keeps, the last one, instead of the
  first.

For a developer: a Flexible's child laid out through an AdaptiveNode keeps the size its arm
declares on Photon, as on the web, and a scroller that a Flexible caps scrolls as far as its capped
viewport needs on every line. Nothing changes on the web.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `flex-layout`: the slot requirement reads the size a child measured to, an AdaptiveNode's arm
  included, and a capped scroller's range is its capped viewport's on every line.

## Impact

- Photon: `Layout/MeasureVisitor.Intrinsics.cs` (`SizedBy`, `MainSizeKind`, `MainSizeIsACeiling`,
  `FlexItem`), `Layout/MeasureVisitor.Flex.cs` (`Slot`, the wrapping pass),
  `Layout/MeasureVisitor.Containers.cs` (`MeasureScrollView`) and a doc comment in
  `Layout/LayoutConstraints.cs`.
- Not reached: the web realizer and the runtime, eqc, the shells, the SDKs and the templates. The
  public surface and the developer surface do not move.
