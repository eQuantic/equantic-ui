# Tasks

## 1. Photon

- [x] 1.1 Under a finger's candidate pan of a table, the host arms the scroller around the table, and once the table takes the pan, `PassOn` moves that scroller by what the table did not follow; the table's point is mapped as the press's frame mapped it; a drag surface in a cell takes the press from the table's candidate. Verify: three of the four new `TableViewportTests` cases fail on the host as it was, and the table, pan, drag, scroll and sheet cases pass (689)

## 2. Web

- [x] 2.1 `table-viewports.ts` passes what the grid did not follow to the scroller around it on each axis, reads the finger against where the pan began on screen, flings what passed on, and leaves a press on a `[data-eq-drag]` to it. Verify: the three new cases of `table-viewport-web.spec.ts` fail on the code as it was, and the runtime suite passes (2866)

## 3. Proved against the real thing

- [x] 3.0 The dashboard's `/table` screen in Chromium at 1280 by 900, with touch pointer events sent to the table. Verify: with the grid at its first row and the page scrolled 36, a finger's pan of 30 toward the bottom scrolls the page to 6; a pan of 40 the grid can follow moves the cells 40 and leaves the page at 6
- [ ] 3.1 The Native.Engine, Web, Server, Compiler and Conformance suites and the runtime suite pass whole

## 4. Documentation

- [x] 4.1 The wiki's TableView page in all five languages, on the wiki branch named like this one
- [x] 4.2 One entry for the ledger citing #1007, as a file of its own under `docs/ledger/`
