# Tasks

## 1. The path on both producers

- [x] 1.1 The server's lowering threads the path the browser's lowering threads, by the segments of `LoweringPath`. Verify: every case of the component parity fixture lowers alike in the browser
- [x] 1.2 A table's and a `Navigable` grid's cells are scoped by the path. Verify: `two-spreadsheets`, `two-unlabelled-tables` and `two-calendars` in the parity fixture, whose reference check refuses an id that names two elements; with the label scope restored it fails on `eq-tcell-lq84u1-1-1` and on `eq-cell-1du8uvv-1-0`
- [x] 1.3 A group table view's reading order, which owns its cells since #1331, is scoped by the path too (#1426). Verify: `two-unlabelled-group-tables` in the parity fixture, on both sides; with the label scope restored for a group alone it fails on `eq-tcell-ztntfp-0-0`
- [x] 1.4 A component's step is one the scope reads past. Verify: the dashboard's `/sheet` wrote `r/0/0/1/…` on the server and `r0/0/1/…` in the browser for its one grid until the step was spelled `~` on both sides and the scope read past it
- [x] 1.5 The suites. Verify: Web, Server and Design pass on the final head, tsc is clean and the runtime's vitest cases pass (three timing specs fail only under a load average near 90, and pass alone); Images, Native.Engine, Compiler and Conformance passed before main was merged and the component's step changed, which reach none of them

## 2. Proved against the real thing

- [x] 2.1 The dashboard sample's `/sheet` in a browser. Verify: the e2e case reads the cells' ids from the server's markup and from the hydrated page, and finds them scoped alike and each one element's
- [x] 2.2 Docs. Verify: the fidelity audit's citations follow the lines the path moved (`DocsIndexTests`), and a ledger entry cites #1115. No wiki page describes how a cell's id is scoped, so none changes
