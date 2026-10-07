# Tasks

## 1. A char's search reaches the runtime

- [x] 1.1 Port `IndexOf(char, int[, int])` and `LastIndexOf(char, int[, int])` from .NET 10's `String.Searching.cs` into `utils/string-search.ts` as `indexOfChar` and `lastIndexOfChar`, under `$eq.text`; proved by the runtime's `string-search.spec.ts`, every answer measured on .NET 10.0.9 with `dotnet fsi`
- [x] 1.2 Route the four overloads from `StringMethodStrategy` through the comparing overloads' runtime call, bound by the method and, with no model, by a char literal; proved by `StringComparisonOverloadTests.ACharSearchWithAStart_GoesToTheRuntime` and `ACharSearchWithNoStart_StaysJavaScripts`
- [x] 1.3 Run both sides through the conformance harness: `InstanceStringComparisonConformanceTests.ACharSearch_ChecksItsStartAndItsCountAsDotNetDoes` (16 cases, 9 of them failing on main)

## 2. Proof and documentation

- [x] 2.1 Run the five suites (Compiler, Web, Server, Conformance, the runtime's `-t:TestRuntime`) and build `samples/DefaultUIDashboard` with no warning
- [x] 2.2 The wiki's SupportedFeatures page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 2.3 One `docs/LEDGER.md` line citing #534
