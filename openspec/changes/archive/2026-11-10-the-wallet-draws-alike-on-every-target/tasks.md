# Tasks

## 1. Photon: a positioned child spans the stack its flow sized

- [x] 1.1 Measure a stack's flow children first and its positioned children against the resolved size; let an axis they span be decided by the stack. Verify: `AFillingChildAnchoredOnBothSides_SpansAStackThatSizesFromItsContent`, its twin through a component, and `ASwipeableRowsAction_IsAsTallAsItsRow` fail on main and pass here
- [x] 1.2 `FillsOf` reads `IsLayoutTransparent`. Verify: `ATransparentWrapper_PassesItsChildsFill_ToAPositionedSpan` fails on main, naming the wrappers the list left out, and passes here
- [x] 1.3 The native suite. Verify: 3377 passed, none failed

## 2. Web: a SafeArea takes its child's size

- [x] 2.1 The C# lowering and its TypeScript twin carry the fill and the cap the child lowered to, through an adaptive node's arms. Verify: the four `ASafeArea…` cases in `WrapperLayoutTransparencyTests`, three of which fail on main
- [x] 2.2 `Fills` reads `IsLayoutTransparent`, and `TheBrowsersFills_WalksEveryTransparentWrapper` pins the TypeScript list. Verify: it fails on main and passes here
- [x] 2.3 The web suite and the runtime's own. Verify: the web suite passes once the audit's citations follow the lines they quote; tsc is clean and 2912 vitest cases pass

## 3. Proved against the real thing

- [x] 3.1 The Wallet's web head at 1100 x 720 in a browser. Verify: the rail is 80 x 720 from the top, the cards page's title is centred at 550 and its action is at the right edge, and at 390 x 844 the bar sits at the bottom of the window
- [x] 3.2 The Wallet's desktop head and its Android build, the row swiped open by the tour. Verify: the "Dispute" action is as tall as its row on both
- [x] 3.3 Docs. Verify: the wiki's `Positioned` section says a filling child spans a stack sized by its content, in all five languages, and a ledger entry cites #1342 and #1343
