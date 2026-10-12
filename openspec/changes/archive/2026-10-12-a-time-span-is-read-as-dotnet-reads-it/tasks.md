# Tasks

## 1. Measure before changing

- [x] 1.1 Measure .NET 10's `TimeSpan.Parse` over 9,305 texts in three cultures whose decimal separators differ, and read its `TimeSpanParse`
- [x] 1.2 Write `TimeSpanTextConformanceTests` and run them against 9b43eb2b: all 36 cases fail

## 2. The reader

- [x] 2.1 Add `utils/timespan-reading.ts` with its vitest cases, read by `timeSpan.parse`
- [x] 2.2 Prove it both ways: the 27,915 answers of the measurement are the reader's, the conformance cases pass, and the runtime's tsc and vitest and the conformance, compiler and Server suites pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issue
