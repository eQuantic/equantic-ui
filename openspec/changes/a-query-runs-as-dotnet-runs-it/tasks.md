## 1. A list's version

- [x] 1.1 Keep a version on a list's array (`utils/list-version.ts`), moved by every change .NET counts, and compare it in a `foreach` over a list, in `List<T>.ForEach` and in every query over a list.
- [x] 1.2 Write a list's element through `$eq.collections.listSet`, which checks the index and moves the version (`Place`).

## 2. A query that runs when it is walked

- [x] 2.1 Walk the source one element at a time in the runtime (`utils/query.ts`) for the operators that have a walk, and stop the answering ones at the element that answers.
- [x] 2.2 Run every other deferred operator's lowering when its query is walked (`$eq.query.defer`), its arguments evaluated when it is called.

## 3. Tests

- [x] 3.1 Conformance tests (`ListVersionConformanceTests`, `LazyQueryConformanceTests`): each row of the two issues, each change of a list, each operator walked, answered and refused, both sides executed.

## 4. Documentation

- [ ] 4.1 The wiki's SupportedFeatures page, in every language it is written in.
- [ ] 4.2 A ledger entry under `docs/ledger/`.
