# Tasks

## 1. The lowering

- [x] 1.1 Move the class emitter's method and body lowering into `MethodLowering`, and have the class emitter call it
- [x] 1.2 Lower a record's and a struct's method, operator, conversion and computed property through it
- [x] 1.3 Rename a verbatim C# keyword that JavaScript reserves, at its declaration and every use

## 2. Checks

- [x] 2.1 `RecordMethodLoweringConformanceTests` executes an async method, an iterator, an out and a ref parameter, a pattern variable in each member kind, a structural comparison, reserved parameter names and a generic method on both sides, in both forms; 13 of its 15 cases fail on main
- [x] 2.2 The transpiled pins do not move, and the diagnostics baseline records EQ2005's new site

## 3. Documentation

- [x] 3.1 Add the `docs/LEDGER.md` line citing #432
- [x] 3.2 Update the wiki's SupportedFeatures page, English and Portuguese, on a wiki branch named like this pull request's
- [x] 3.3 Archive this change before the merge
