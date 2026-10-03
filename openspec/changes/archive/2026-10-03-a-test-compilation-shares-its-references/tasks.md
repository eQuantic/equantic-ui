# Tasks

## 1. One reference per assembly

- [x] 1.1 Add `tests/Shared/TestReferences.cs`, link it into the test projects that compile C#, and route every test call site through it. Verify: the solution builds with no `MetadataReference.CreateFromFile` left under `tests/` but the owner's
- [x] 1.2 Add `TestReferencesGuardTests`, failing on a `CreateFromFile` outside the owner. Verify: it fails with one copy put back, naming the file, and passes without it

## 2. Measured

- [x] 2.1 Measure each suite's peak test-host memory and duration before and after, with the suites run alone. Verify: the table in the pull request, from a sampler reading every test host once a second
- [x] 2.2 Run the compiler suite repeatedly under load with the change in, for #473. Verify: the count of runs and of aborted ones, read from each run's exit code and its `Test Run Aborted` line

## 3. Documentation

- [x] 3.1 One `docs/LEDGER.md` line citing #481
