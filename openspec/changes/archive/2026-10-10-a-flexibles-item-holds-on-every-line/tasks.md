# Tasks

## 1. Measure before changing

- [x] 1.1 Lay out the C# web realizer's HTML in Chrome 154: a `Pinned(ScrollView { Width = 400 })` in a share of 300 (wrapper and scroller at 300), a horizontal scroller of 400 around 800 of content in that share (300 wide, a range of 500), zero weights at a basis of 540 around a 400 and a 600 box on wrapping lines that hold still (items of 540, the next item at 540), a weight of one at an exact basis of 540 (an item of 540), and a column 400 tall with a fixed 100 and a zero weight at 540 (300)
- [x] 1.2 Lay the same trees out on Photon at 7d8325275: the scroller under its wrapper at 400, the unwrapped horizontal range at 400, the wrapping items at 400 and 600 with the next at 400, and the column's zero weight at 540

## 2. An item and its scroller, on every line

- [x] 2.1 Carry `WidthIsACeiling` from the slot through transparent wrappers to the scroller, which caps its width while it measures
- [x] 2.2 Build each Flexible's item in the wrapping pass with `FlexItem`, at its resolved size or its basis
- [x] 2.3 Prove it both ways: the new cases of `FlexLayoutTests`, `FlexZeroWeightLayoutTests` and `FlexBasisWrapLayoutTests` fail against 7d8325275 (the scroller at 400, the items at 400) and pass with the fix, and the horizontal range measures 400 before and 500 after; the native layout classes, the Studio walk, the truncation contract and the goldens pass, 253 of 253, and no golden moves

## 3. Documentation

- [x] 3.1 Scope the zero-weight shrink contract to rows and name the column gap in the flex-layout spec, `Flexible.Shrink`, `FlexNode`, the flex pass's comment, and the wiki's Photon page in English and Portuguese
- [x] 3.2 The `docs/LEDGER.md` line of this pull request says what the last round found
- [x] 3.3 Run the docs and wiki guards against the branch and its wiki branch
