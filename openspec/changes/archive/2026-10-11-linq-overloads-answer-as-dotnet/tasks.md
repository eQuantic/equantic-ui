# Tasks

## 1. Measure before changing

- [x] 1.1 Write `LinqOverloadConformanceTests`, 69 cases executed on both sides: a default value with and without a predicate, a null element, evaluation order, `SingleOrDefault`'s second element and second match; a selector named first and a string under a null-conditional; pairs, triples and deconstruction; the mean of longs, nullables, an empty sequence and an overflow; `SequenceEqual` with the comparers that ask for the default, longs, decimals by value, `NaN`, arrays by reference and records by value
- [x] 1.2 Run them against a7d5f3d: 50 of the 69 fail, with the `TypeError`, the BigInt mix and the truthy array the issues describe; the 19 that pass are the neighbours each overload was taken for, kept as pins

## 2. The twins and the calls

- [x] 2.1 Add `average`, `firstOrDefault`, `lastOrDefault`, `singleOrDefault`, `sequenceEqual` and `zipTuples` to `utils/linq.ts` and `$eq.linq`, with their vitest cases
- [x] 2.2 Hand each bound overload to its twin through `LinqArguments`, by the parameter each argument binds to, the selector form of `Zip` included, find a bound argument under a null-conditional through the one written in the tree, and fence `SequenceEqual`'s comparer
- [x] 2.3 Type the accumulator of `GroupBy` and `ToLookup` in a type-checked module, proved by the `BoundReceivers` fixture through the runtime's strict `tsc` (red with the typing removed)
- [x] 2.4 Prove it both ways: the conformance suite passes, 5156 of 5157 with one skipped; the 15 cases the review added fail 12 to 3 on this change's first head; the null-argument baseline only shrinks, by 36; the compiler, Web, Server and Design suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures and Compiler pages, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issues
