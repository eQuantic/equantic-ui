# Tasks

## 1. The path on both producers

- [x] 1.1 The server's lowering threads the path the browser's lowering threads, by the segments of `LoweringPath`. Verify: every case of the component parity fixture lowers alike in the browser
- [x] 1.2 A table's and a `Navigable` grid's cells are scoped by the path. Verify: `two-spreadsheets`, `two-unlabelled-tables` and `two-calendars` in the parity fixture, whose reference check refuses an id that names two elements; with the label scope restored it fails on `eq-tcell-lq84u1-1-1` and on `eq-cell-1du8uvv-1-0`
- [ ] 1.3 The suites. Verify: Web, Server, Images, Design, Native.Engine, Compiler and Conformance pass, tsc is clean and the runtime's vitest cases pass

## 2. Proved against the real thing

- [ ] 2.1 The dashboard sample's `/sheet` in a browser. Verify: the e2e case reads the cells' ids from the server's markup and from the hydrated page, and finds them scoped alike and each one element's
- [x] 2.2 Docs. Verify: the fidelity audit's citations follow the lines the path moved (`DocsIndexTests`), and a ledger entry cites #1115. No wiki page describes how a cell's id is scoped, so none changes
