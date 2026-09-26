# Tasks

## 1. List.Remove

- [x] 1.1 Lower `Remove` to `$eq.collections.remove`, which answers a bool and compares as `EqualityComparer<T>.Default`
- [x] 1.2 Check: `ListRemoveConformanceTests` run on both sides, the first cases to execute a List's Remove, failing before

## 2. Bool text

- [x] 2.1 Read `bool.Parse`, `bool.TryParse` and `Convert.ToBoolean(string)` through `$eq.bool.*`, with .NET's trimming, ASCII fold and exceptions
- [x] 2.2 Join bool to the numeric Parse/TryParse lowering and remove `BooleanMethodStrategy`
- [x] 2.3 Point the number reader at `utils/white-space`
- [x] 2.4 Check: `BooleanTextConformanceTests` run on both sides; 23 of the PR's 27 cases fail on main

## 3. Documentation

- [x] 3.1 The wiki's SupportedFeatures and Compiler pages in English and Portuguese, published at the merge
- [x] 3.2 One `docs/LEDGER.md` line citing #400 and #402
- [x] 3.3 Archive this change in the same pull request
