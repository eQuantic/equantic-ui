# Tasks

## 1. Measure before changing

- [x] 1.1 Measure every `Split` overload, `IndexOf` and `LastIndexOf` from a start and for a count, and `StringBuilder.Append` of a float, a double and an enum on .NET 10, and read `String.Manipulation.cs`'s `SplitInternal` for the order of its checks
- [x] 1.2 Write `StringSplitAndSearchConformanceTests` and the builder's `AValueTheBuilderIsHanded_PrintsAsDotNet`, and run them against 18509220: 55 of the 73 split and search cases fail as #753 and #724 describe, and the builder's 10 direct values already pass since #637; the rows for an `object` holding a float or an enum fail through boxing, which moves to #768

## 2. The twins and the calls

- [x] 2.1 Add `split` and `splitAny` to the new `utils/string-split.ts` and `$eq.text`, with their vitest cases, and remove `splitOnWhiteSpace`, which `splitAny` with no set replaces
- [x] 2.2 Hand each `Split` to its twin by the bound overload, the count and the options in their parameters' places, the chars of a params parameter as an array, and a span handed whole refused; hand `IndexOf` and `LastIndexOf` of a string from a start and for a count to the runtime's ordinal searches
- [x] 2.3 Prove it both ways: the new conformance cases pass and the whole suite with them, the null-argument baseline only shrinks, by 10, the BCL audit moves ten verdicts to `eq`, the shared components' pinned modules change only where they split, and the compiler, Web, Server and Design suites and the runtime's tsc and vitest pass

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures page, in English and Portuguese, on the wiki branch of the same name
- [x] 3.2 One `docs/LEDGER.md` line citing the issues
