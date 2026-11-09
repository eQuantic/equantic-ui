# Tasks

## 1. Web

- [x] 1.1 `table-viewports.ts` takes what the scroller shows from where the viewport starts, read in the scroller's content, and the whole extent when that is under half. Verify: two of the three new cases of `table-viewport-web.spec.ts` fail on the rule as it was, and the runtime suite passes

## 2. Photon

- [x] 2.1 A `ScrollView` collects the viewports that take its extent, reads where each starts after measuring its content, and measures again once when one moved; the scroll model keeps where it started. Verify: the case of a table below the top of its scroller fails on the rule as it was, the rest of `TableViewportTests` and the perf harness pass

## 3. Proved against the real thing

- [x] 3.1 The dashboard's `/table` screen in Chromium at 1280 by 900: the table's bottom edge and its horizontal scrollbar stand at the page's visible end. Verify: the measured gap, before and after. Measured: the page shows 844 and the table starts 36 into it. On the rule as it was the table took 844 and ended 36 below the fold, and now it takes 808 and ends at the fold
- [ ] 3.2 The Native.Engine, Web, Server, Compiler and Conformance suites and the runtime suite pass whole

## 4. Documentation

- [x] 4.1 The wiki's TableView page in all five languages, on the wiki branch named like this one
- [x] 4.2 `docs/SPREADSHEET-PLAN.md` says the rule as it is
- [x] 4.3 One entry for the ledger citing #1006, as a file of its own under `docs/ledger/`
