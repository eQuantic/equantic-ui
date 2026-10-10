# Proposal

Part of #680, a Bug under #166 (Geometry and semantics live in the vocabulary): it answers Copilot's
third and last round on the pull request (review 5479006584), two defects in code this pull request
added and a contract stated wider than Photon keeps it.

## Why

Measured with the web realizer's own HTML in Chrome 154, and on Photon at 7d8325275:

- `Pinned(ScrollView { Width = 400 })` in a share of 300: Chrome draws the wrapper and the scroller
  at 300. Photon capped the wrapper and left the scroller painting and clipping at 400, because the
  slot capped the child's bounds after it measured, and only the child's. Even unwrapped, a
  horizontal scroller of 400 around 800 of content, which Chrome draws at 300 with a scroll range of
  500, kept a range of 400 on Photon.
- In a wrapping row whose line neither grows nor shrinks, Chrome gives `Flexible(400 box, flex: 0,
  basis: 540)` an item of 540 with the box inside at 400 (a 600 box overflows its 540 item), starts
  the next item at 540, and gives a weight of one at an exact basis of 540 an item of 540. Photon had
  no item in the wrapping pass: the box was the item, so it occupied 400 or 600, and the next item
  started at 400.
- A column 400 tall holding a fixed 100 and `Flexible(box, flex: 0, basis: 540)`: Chrome shrinks the
  zero weight to 300; Photon leaves it at 540, 240 past the column's end. Photon's single-line pass
  takes nothing back from an overflowing column for any item, which predates this pull request.

## What Changes

- The slot tells the child that its declared width is a ceiling, and the word travels through
  layout-transparent wrappers to the scroller, which caps its width while it measures, before its
  scroll range is taken.
- The wrapping pass builds each Flexible's item the slot's way, at the size its line resolved or its
  basis when the line holds still, the child keeping a size it declares.
- The zero-weight shrink contract is stated for rows, and the docs name the column gap: the
  flex-layout spec, `Flexible.Shrink`, `FlexNode`, the flex pass's own comment and the wiki's Photon
  page in English and Portuguese. The vertical path is not built here.

For a developer: a scroller of a fixed width inside a Flexible is drawn, clipped and scrolled at the
item's width on Photon, a Flexible on a wrapping line occupies its basis, and a column that
overflows on Photon is documented as overflowing. Nothing changes on the web.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `flex-layout`: the slot requirement holds on a wrapping line too and its scroller ceiling reaches
  through wrappers, and the zero-weight shrink requirement is scoped to rows.

## Impact

- Photon: `Layout/LayoutConstraints.cs` (`WidthIsACeiling`, internal), `Layout/MeasureVisitor.Containers.cs`
  (`MeasureScrollView`), `Layout/MeasureVisitor.Flex.cs` (`Slot`, the wrapping pass, a comment) and
  `Layout/MeasureVisitor.Intrinsics.cs` (`FlexItem`).
- Primitives: doc comments on `Flexible.Shrink` and `FlexNode`.
- Not reached: the web realizer and the runtime, eqc, the shells, the SDKs and the templates. The
  public surface and the developer surface do not move: the constraint flag is internal.
- Left for an issue of its own: the vertical shrink path of Photon's single-line pass.
