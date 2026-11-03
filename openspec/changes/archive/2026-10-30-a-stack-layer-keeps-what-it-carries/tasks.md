# Tasks

## 1. Measure before changing

- [x] 1.1 Lay out the web realizer's HTML in Chrome 152: a hugging stack beside a wider sibling and a column of them (0 wide, the content overflowing), and a filling and a hugging scroller, and a hugging column around one, over 4000 of content in a stack in a box 482 wide (482)
- [x] 1.2 Render a `Positioned { Bookmark, Key, Origin }` in a stack on the server and in the twin: no id, no key, no origin

## 2. A layer keeps what it carries

- [x] 2.1 Write a cell's minimum to zero only on an axis the stack caps its layer on, in both producers, with `Fills` answering for a `ScrollView`
- [x] 2.2 Decorate the anchor with the Positioned's key, bookmark and origin, in both producers
- [x] 2.3 Prove it both ways: `StackRealizerTests`, `stack.spec.ts` and the parity fixture, and the same HTML in Chrome 152: 44 where it was 0, and the scrollers still 482
- [x] 2.4 Check the code editor's real layer in the dashboard sample, the case the minimum was written for

## 3. Documentation

- [x] 3.1 One ledger entry under `docs/ledger/` citing #698 and #940; the wiki states neither gap, so no page changes
