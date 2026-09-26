# Tasks

## 1. The lowering

- [x] 1.1 Move the class emitter's method and body lowering into `MethodLowering`, and have the class emitter call it
- [x] 1.2 Lower a record's and a struct's method, operator, conversion and computed property through it
- [x] 1.3 Rename a verbatim C# keyword that JavaScript reserves, at its declaration and every use

## 2. Checks

- [x] 2.1 `RecordMethodLoweringConformanceTests` executes an async method, an iterator, an out and a ref parameter, a pattern variable in each member kind, a structural comparison, reserved parameter names and a generic method on both sides, in both forms (15 cases when this step closed, 13 of them failing on main; 4.4 has the class as it merged)
- [x] 2.2 The transpiled pins do not move, and the diagnostics baseline records EQ2005's new site

## 3. Documentation

- [x] 3.1 Add the `docs/LEDGER.md` line citing #432
- [x] 3.2 Update the wiki's SupportedFeatures page, English and Portuguese, on a wiki branch named like this pull request's
- [x] 3.3 Archive this change before the merge

## 4. Review

- [x] 4.1 Ask whether a method is async of its return type's symbol, in every emitter
- [x] 4.2 Lower an accessor's block as a method's body: a getter's iterator, an out var's local
- [x] 4.3 Lower a C# 14 extension member through the same method lowering, its receiver in front
- [x] 4.4 Check: the conformance class grows to 20 cases, 15 of the first 19 failing on main and the 20th without its fix, and the compiler suite holds the class, component and extension paths the harness cannot run
- [x] 4.5 File the two type-annotation functions that feed the lowering (#461)
- [x] 4.6 Declare a variable a pattern binds by the name its uses have, and file the other declarations that skip it (#467)
