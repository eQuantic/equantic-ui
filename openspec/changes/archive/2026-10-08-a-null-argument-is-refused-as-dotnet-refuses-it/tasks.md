# Tasks

## 1. The measurement

- [x] 1.1 Derive the surface from the BCL audit's committed record: its types, their members by reflection, and eqc's own verdict per probe
- [x] 1.2 One compilation and one program per side, each probe's call bound to the member it names, and a control without the null
- [x] 1.3 Compare the exception's type, its `ParamName`, its message where the SDK composes it, and a returned value
- [x] 1.4 A baseline that may only shrink, each entry with its aspect and its reason, regenerated behind `EQ_UPDATE_NULL_ARGUMENT_BASELINE=1`
- [x] 1.5 A guard over the audit's claims, and checks of the comparison and of the browser's reading of a throw

## 2. The twins

- [x] 2.1 Measure .NET 10's refusals with `dotnet fsi`: the parameter each names, and the order of the checks
- [x] 2.2 LINQ's `Max`, `Min` and `ToDictionary`, a sequence named by its parameter, `CompareTo(object)`, `new Guid(text)`, `GetUnicodeCategory` and the cancellation pair
- [x] 2.3 Prove it on both sides: the theory fails on the base for the 67 probes it closes, and the cases one canonical value cannot reach fail there too

## 3. Documentation and the suites

- [x] 3.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 3.2 `docs/LEDGER.md`: one line citing #569
- [x] 3.3 The full suites, each alone and read by its exit code: Compiler, Web, Server, Conformance, and the runtime's `TestRuntime`
