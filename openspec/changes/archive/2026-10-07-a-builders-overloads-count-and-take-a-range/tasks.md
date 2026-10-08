# Tasks

## 1. A builder's counted and ranged overloads answer as .NET's do

- [x] 1.1 Measure .NET 10's answer and refusal for each overload shape of `Append`, `Insert`, `Replace`, `Remove` and `ToString`, and the order of their checks
- [x] 1.2 The runtime: one method per shape by its count of arguments, the `char[]` overloads as methods of their own, every refusal in .NET's words and order
- [x] 1.3 eqc: `StringBuilderStrategy` names the `char[]` overloads from the overload the call binds
- [x] 1.4 Prove it on both sides in `StringBuilderConformanceTests`, failing on the base, the two-line refusals compared except for the host's newline

## 2. Documentation and the suites

- [x] 2.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 2.2 `docs/LEDGER.md`: one line citing #650
- [x] 2.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
