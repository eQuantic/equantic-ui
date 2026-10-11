# Tasks

## 1. Measure before changing

- [x] 1.1 Lay out the C# web realizer's HTML in Chrome 154: a fixed 400 box in a zero weight shrunk to 300 (400, from the item's start), the same box in a weighted share of 300 (400), a fixed 100 box in that share (100), and a centred Text in it (an inline span as wide as its content, at the item's start)
- [x] 1.2 Lay the same trees out on Photon at 2cbdbb1c9: every child at 300

## 2. The slot sizes the item, and a fixed child keeps its own size

- [x] 2.1 In `Slot`, pin the wrapper to the slot, and the child only when it declares no main size of its own (`MainSizeKind`, the main-axis twin of `CrossSizeKind`)
- [x] 2.2 Prove it both ways: the child's bounds in `FlexZeroWeightLayoutTests` and the new case of `FlexLayoutTests` fail before the fix (300 where Chrome gives 400) and pass with it, and two wrong rules each fail one assertion (keeping only a wider child leaves the 100 box at 300, keeping every child shrinks a Text's line box to its 31.8)
- [x] 2.3 Run the native layout classes that hold a Flexible, the Studio walk, the truncation contract and the goldens: all pass, and no golden moves

## 3. Documentation

- [x] 3.1 `Flexible.Shrink` states the rule as it is, and WriteOnceComponents and Photon say it in English and Portuguese, in one commit on the wiki branch
- [x] 3.2 The `docs/LEDGER.md` line of this pull request says it
- [x] 3.3 Run the docs and wiki guards against the branch and its wiki branch
