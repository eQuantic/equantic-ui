# Tasks

## 1. Measure before changing

- [x] 1.1 Write `LinqOverloadConformanceTests`, 53 cases executed on both sides: a default value with and without a predicate, a null element, evaluation order; pairs, triples and deconstruction; the mean of longs, nullables, an empty sequence and an overflow; `SequenceEqual` with the comparers that ask for the default, longs, decimals by value, `NaN`, arrays by reference and records by value
- [x] 1.2 Run them against a7d5f3d: 37 of the 53 fail, with the `TypeError`, the BigInt mix and the truthy array the issues describe; the 16 that pass are the neighbours each overload was taken for, kept as pins

## 2. The twins and the calls

- [x] 2.1 Add `average`, `firstOrDefault`, `lastOrDefault`, `sequenceEqual` and `zipTuples` to `utils/linq.ts` and `$eq.linq`, with their vitest cases
- [x] 2.2 Hand each bound overload to its twin through `LinqArguments`, by the parameter each argument binds to, and fence `SequenceEqual`'s comparer
- [x] 2.3 Type the accumulator of `GroupBy` and `ToLookup` in a type-checked module, proved by the `BoundReceivers` fixture through the runtime's strict `tsc` (red with the typing removed)
- [x] 2.4 Prove it both ways: the conformance cases pass, 5140 of 5141 with one skipped; the null-argument baseline only shrinks; the compiler, Web, Server and Design suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures and Compiler pages, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issues
