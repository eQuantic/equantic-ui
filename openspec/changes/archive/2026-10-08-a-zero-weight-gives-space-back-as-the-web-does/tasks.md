# Tasks

## 1. Measure before changing

- [x] 1.1 Lower each case through the C# web realizer and lay its HTML out in Chrome 154: a zero weight around a 400-wide box after a rigid 100 in 400 (300, at a basis and without one), `shrink: 3` and `shrink: 1` at 540 in 1000 (480 and 520), `shrink: 3` beside a box that fills the row (206.11 and 793.89), a zero weight holding text beside one holding a box (200 and 200), and two holding text with factors 3 and 1 (136.37 and 163.63)
- [x] 1.2 Lay the same trees out on Photon at 95f952b2f: 400 with the line 100 past its row, 500 and 500, 473.68 and 526.32, 100 and 300, 180 and 120
- [x] 1.3 Measure what lies beyond a zero weight, to report rather than change: a box of words beside one, and a Flexible that must shrink in a wrapping row (300 in Chrome, 400 on Photon)

## 2. A zero weight gives space back as the web does

- [x] 2.1 Leave a zero weight out of the text cut, whatever it holds
- [x] 2.2 In the flex-shrink pass, give a zero weight a floor of zero and a weight of its shrink times its size, settled by CSS's loop; every other item keeps its floor and its room
- [x] 2.3 Re-measure a shrinking zero weight in a slot, with a basis or without one
- [x] 2.4 Prove it both ways: the three new cases of `FlexZeroWeightLayoutTests`, which take Chrome's numbers, fail against the code before the fix and pass with it, and the native layout classes that hold a Flexible pass; a case in the component parity fixture pins the shrink factor's declaration on both web sides
- [x] 2.5 Lay the measured trees out on Photon again: all seven take Chrome's numbers

## 3. Documentation

- [x] 3.1 `FlexNode.Wrap` states the contract and where Photon does not follow it yet (#728), and `MeasureFlexWrapped`'s comments say what it does today
- [x] 3.2 The audit's three citations into the flex pass point at the lines the fix moved them to, and the `docs/LEDGER.md` line of this pull request says what changed
- [x] 3.3 Run the docs and wiki guards against the branch and its wiki branch, with the web suites that build a Flexible
