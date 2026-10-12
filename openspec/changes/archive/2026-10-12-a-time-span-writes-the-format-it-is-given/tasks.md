# Tasks

## 1. Measure before changing

- [x] 1.1 Measure the standard and custom formats of a TimeSpan on .NET 10, every value from zero to `MinValue`, in six cultures, and what each refusal says
- [x] 1.2 Write `TimeSpanFormatConformanceTests` and run them against d5097bc2: 45 of the 50 cases fail as #837 describes

## 2. The writer and the calls

- [x] 2.1 Add `utils/timespan-text.ts` with its vitest cases, read by the formatter and by the twin's own `toString`
- [x] 2.2 Leave a TimeSpan's `ToString` to the formatter's strategy, its provider read as a date's is
- [x] 2.3 Prove it both ways: the new conformance cases pass and the whole suite with them, the compiler's new cases fail without the change, the null-argument baseline only shrinks, by one, the shared components' pinned modules do not change, and the compiler, Web and Server suites and the runtime's tsc and vitest pass
- [x] 2.4 Review the whole diff before the pull request, and fix what it finds
- [x] 2.5 Read a named provider by the parameter it binds to (found in review): the 7 cases added for a TimeSpan, a date and a number fail before the fix and pass after it

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issue
