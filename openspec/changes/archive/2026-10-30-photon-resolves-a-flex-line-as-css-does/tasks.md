# Tasks

## 1. Measure before changing

- [x] 1.1 Lay out the C# web realizer's HTML in Chrome 152: a basis of 540 beside a weight of the same size (990 | 450), weights 2 and 1 over bases of 100 and 200 beside a fixed 100 (500 | 400 | 100), a weighted basis of 540 after a fixed 100 in 400 (300, and 540 with `shrink: 0`), two weights without a basis around 600 boxes in a wrapping row of 1000 (500 | 500 on one line, 495 and 505 with a gap of 10), a column 400 tall with a fixed 100 and a zero weight at 540 (300), and a row 100 tall that stretches a Box, Stack, Canvas, CameraPreview, ScrollView and WebFrame 20 tall (20 each)
- [x] 1.2 Lay the same trees out on Photon at af3f714e0: 720 | 720 and 600 | 300 | 100, the wrapping weights on two lines, the column's zero weight at 540, and the five node types after the Box stretched to 100

## 2. The line resolves from its starts

- [x] 2.1 Start every item of the single-line pass from its flex base size, share the leftover on top by weight, and share only the fraction of it that weights under one add up to
- [x] 2.2 Take an overflow back from every Flexible by its shrink times its start, in a column as in a row, the text cut staying a row's
- [x] 2.3 Break a weighted Flexible without a basis on nothing in the wrapping pass, and count the gap between items that ask for nothing
- [x] 2.4 Answer both axes with one classifier, `DeclaredSizeKind`, asked of what a child measured to, through a component to what it built; remove `CrossSizeKind` and `WidthKind`
- [x] 2.5 Prove it both ways: `FlexLineResolutionTests` fails against af3f714e0 and passes with the fix, and the native engine suite passes with no golden moving

## 3. Documentation

- [x] 3.1 `Flexible.Basis`, `Flexible.Shrink` and `FlexNode.Wrap` say what Photon now does; the wiki states none of the four gaps (its Photon page names only the min-content floor, which still holds for every item but a Flexible), so no page changes
- [x] 3.2 One ledger entry under `docs/ledger/` citing #693, #728, #744 and #747
- [x] 3.3 Run the docs guards and `openspec validate --all --strict` against the branch
