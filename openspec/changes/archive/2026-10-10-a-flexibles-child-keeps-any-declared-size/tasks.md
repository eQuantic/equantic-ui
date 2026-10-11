# Tasks

## 1. Measure before changing

- [x] 1.1 List the vocabulary's node types by reflection: 41 concrete ones, 14 declaring a width or a height, of which the classifier read 9
- [x] 1.2 Lay out the C# web realizer's HTML in Chrome 154: each of the 14, declared 400 and 100, in a share of 300, plus a 320 camera preview in a share and in a shrunk zero weight, and a box as wide as the window less 32. Every one keeps its declared width, except a ScrollView of 400, which is 300
- [x] 1.3 Lay the same trees out on Photon at 8b326209b: the camera preview, the stack, the scroller, the canvas, the web frame and the window-wide box at 300

## 2. Every declared size is read

- [x] 2.1 Make `MainSizeKind` read every node type that declares a main size, and stop asking `CrossSizeKind`
- [x] 2.2 Keep a declared or window-relative size in the Slot, and a ScrollView's width only up to the slot (`MainSizeIsACeiling`)
- [x] 2.3 Add `MainSizeKindCoverageTests`, which enumerates the vocabulary and asks about each declared size, and layout cases with Chrome's numbers in `FlexLayoutTests` and `FlexZeroWeightLayoutTests`
- [x] 2.4 Prove it both ways: against the classifier at 8b326209b, 12 of 58 fail (the five unread types on both axes, and the two layout cases); a classifier with only the camera preview added fails the coverage test eight times; without the ceiling the 400 scroller is 400, and with Fixed alone the window-wide box is 300. With the fix, 250 of 250 pass across the native layout classes, the Studio walk, the truncation contract and the goldens, and no golden moves

## 3. Documentation

- [x] 3.1 The `docs/LEDGER.md` line of this pull request says what the second round found
