## 1. The reader

- [x] 1.1 The ISO form probed where a date may start, in the Gregorian calendar, its clock and zone in place of earlier ones (#1841)
- [x] 1.2 A zone before anything; a second zone refused plainly
- [x] 1.3 A day's name once at every boundary, asked of the date (or today) last (#1842)
- [x] 1.4 Two numbers of three digits or more refused (#1843)
- [x] 1.5 `DateOrderConformanceTests`, both sides executed, in the invariant culture, en-US, th-TH, ar-SA, pt-BR and sv-SE: all 11 fail on main's reader and pass on this one

## 2. Proof and documentation

- [x] 2.1 The reader's spec pins each rule
- [x] 2.2 The full local suites, Conformance whole, and `TestRuntime`
- [x] 2.3 One ledger entry under `docs/ledger/` citing #1841, #1842 and #1843
