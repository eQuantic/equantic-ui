## 1. The kept parameters, counted once

- [x] 1.1 `DependencyBesideAnInitializerConformanceTests`: a named, a positional and no argument after a dependency, and a dependency in the middle, each beside an initializer, read on both sides in both modes; they fail on #1825's branch
- [x] 1.2 A pin in `ConstructorInjectionTests` of the construction the twin is built with for four of them
- [x] 1.3 `LeavesOut`, the one rule the drop of a call's arguments and `FillToArity` both read

## 2. Documentation and the suites

- [x] 2.1 A ledger entry under `docs/ledger/`
- [x] 2.2 Every test project of the solution with the Conformance suite in full, and the runtime's `tsc` and tests, each read by its exit code
