# Tasks

## 1. The warm compilation

- [x] 1.1 `AppBuild.LoadFrom(previous)`: keep the compilation, replace in place the trees whose text changed, and answer false for anything a cold load reads differently. Verify: `AWarmBuildTests`, a warm build equal to a cold one after the same edit, and the case fails with the replacement removed
- [x] 1.2 The served eqc starts each compile from the last, and loads the project before the first request
- [x] 1.3 The compiler's suite, its component pins included, passes on the restructured load. Verify: 2813 tests

## 2. The proof

- [x] 2.1 The conformance suite passes. Verify: 7374 tests
- [x] 2.2 The measurement on the dashboard sample, with the load average beside each number. Verify: eqc's median from 5.5s (load 35 to 38) to 3.6s (load 27 to 31), and six saves in a row hydrated at a load of 42 to 49
- [ ] 2.3 Lower the web's ceiling by what was measured, once #1261 has set it from CI

## 3. Documentation

- [x] 3.1 One entry for the ledger citing #1278, as a file of its own under `docs/ledger/`
- [x] 3.2 The wiki's hot reload paragraph in five languages
