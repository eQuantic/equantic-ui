# Tasks

## 1. A builder has the members a page reaches

- [x] 1.1 Measure .NET 10's answers and refusals for AppendFormat, AppendJoin, Capacity through every edit, MaxCapacity, EnsureCapacity, the indexer, Length, Equals and CopyTo
- [x] 1.2 The runtime: the chunk sizes .NET would allocate, and every member with its refusals
- [x] 1.3 eqc: AppendFormat and AppendJoin through string.Format's and string.Join's lowering, the indexer carried by the twin, Equals(StringBuilder), and the refusals at the build
- [x] 1.4 Prove it on both sides in `StringBuilderConformanceTests`, failing on the base
- [x] 1.5 The review's findings, measured on .NET 10: named arguments, the interpolated appends part by part, `AppendLine`, `Append(StringBuilder)`, a `Replace` past the maximum, and the browser's longest string

## 2. Documentation and the suites

- [x] 2.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 2.2 `docs/LEDGER.md`: one line citing #679
- [x] 2.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
