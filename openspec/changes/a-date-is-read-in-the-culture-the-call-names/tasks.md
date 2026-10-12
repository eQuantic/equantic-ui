# Tasks

## 1. Measure before changing

- [x] 1.1 Measure how .NET 10 reads a date, a time and a time span in ten cultures, each refusal's words included, and what `Convert.ToDateTime` answers for each overload
- [x] 1.2 Write `DateTextInTheCultureConformanceTests` and run them against c25b177e: 242 of the 280 cases, in ten cultures, fail as #806, #717 and #844 describe

## 2. The reader and the calls

- [x] 2.1 Add `utils/date-reading.ts` with its vitest cases, read by the five twins' `parse` and `tryParse`
- [x] 2.2 Claim every overload of `Parse` and `TryParse` in eqc by the parameter each argument binds to, the provider read as the culture it names, and leave the exact readers to the fence
- [x] 2.3 Read `Convert.ToDateTime` by its overload (#844)
- [x] 2.4 Prove it both ways: the new conformance cases pass and the whole suite with them, 35 of the compiler's 45 new cases fail without the change, the null-argument baseline only shrinks, the shared components' pinned modules do not change, and the compiler, Web and Server suites and the runtime's tsc and vitest pass
- [x] 2.5 Review the whole diff before the pull request, and fix what it finds: a clock written with points, the ISO form, the order of the parts and `Convert`'s words for a value held in an object, which 49 of the 280 cases fail without; and the `transpiler-bcl` requirement that still read `TimeOnly.Parse` in the invariant grammar

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issues, and EQ2108's row in `docs/DIAGNOSTICS.md`
