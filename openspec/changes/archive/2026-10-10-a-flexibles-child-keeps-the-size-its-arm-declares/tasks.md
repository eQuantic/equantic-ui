# Tasks

## 1. Measure before changing

- [x] 1.1 Lay out the C# web realizer's HTML in Chrome 154: in a share of 300, an AdaptiveNode's box arm of 400 (400) and of 100 (100), a scroller arm 400 wide around 800 of content (300, a range of 500) and one 100 wide (100, a range of 700); in a wrapping row of 300, `Flexible(ScrollView { Width = 400 }, flex: 1)` around 800 of content with a basis of 300 and of 200 (300, a range of 500)
- [x] 1.2 Lay the same trees out on Photon at f57db061e: the four arms at 300, the capped scroller arm with a range of 400, and the wrapped scroller at 300 with a range of 400

## 2. The size of what was measured, and the range of what is drawn

- [x] 2.1 Walk the measured tree to the node whose size a child is (`SizedBy`), and ask the size classifiers of that node in `FlexItem`, the slot and the wrapping pass
- [x] 2.2 Measure the slot's child again under the ceiling when it measured to a scroller wider than the item
- [x] 2.3 Register the scroll range of the measure the tree keeps
- [x] 2.4 Prove it both ways: the new cases of `FlexLayoutTests` and `FlexBasisWrapLayoutTests` fail against f57db061e and pass with the fix; `MainSizeKindCoverageTests` reads a declared size through every node type that takes its child's size, and finds the arm in an AdaptiveNode's place

## 3. Documentation

- [x] 3.1 The flex-layout spec says the slot reads the size a child measured to, an arm included, and that a capped scroller scrolls by its capped viewport on every line
- [x] 3.2 The `docs/LEDGER.md` line of this pull request says what the rehearsal found
