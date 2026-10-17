# Tasks

## 1. Measure

- [x] 1.1 Measure on main (1695e2c98), through the build loop eqc runs, a kept-out type named only in a base list, a component's header and a class's, in the app and from a library: both build, and neither module names it
- [x] 1.2 Measure #944's last commit both ways: its new cases fail on the source of the commit before it (25 of 225) and pass on its own (225 of 225)

## 2. The spec says what the build does

- [x] 2.1 The `client-boundary` delta: the enumerator's protocol in a `foreach`, a spread and a LINQ operator, the variance of a receiver, a base list's type argument, a dictionary's and a string literal written as nothing, each with the scenario a test of #944 or of this change executes
- [x] 2.2 `openspec validate --strict` passes on the change

## 3. Pin the base list

- [x] 3.1 `ServerOnlyTypeOfTheAppTests.AKeptOutTypeArgumentOfABaseList_IsWrittenAsNothing`, for a component's header and a class's, in the app and from a library, run through the build loop

## 4. Prove and document

- [x] 4.1 Run the Compiler suite
- [x] 4.2 The ledger: #944's entry names what its last commit refuses, and one entry for this change under `docs/ledger/`
- [x] 4.3 The wiki pages that state the boundary, in every language each is written in, in one commit on the branch of this name
- [x] 4.4 Archive the change with `openspec archive the-boundary-spec-says-what-the-build-does --yes`
