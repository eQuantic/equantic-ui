# Tasks

## 1. The names the emitted code declares and reads

- [x] 1.1 Every binding a lowering declares takes a `$`; proved by `EmittedBindingNamesTests`, which reads the compiler's source and fails on a binding without one
- [x] 1.2 `StringExtensions.EmittedGlobals` is the one list of the globals the output reads, and `ToJsIdentifier` gives a `$` to a local named like one; proved by `EmittedGlobalsTests`, which derives the globals from the compiler's source
- [x] 1.3 The conformance cases on a local named `crypto`, `undefined` and `Math`, and on a captured `_sum`, `key`, `a` and a sequence named `x`, failing on main

## 2. A value C# evaluates once

- [x] 2.1 `JsExprWriter` binds a part whose hole sits inside a function the template defines, a lambda written in place excepted
- [x] 2.2 `Sum`, `Average`, `OrderBy`, `GroupBy`, `Distinct` and the lookup's indexer on the IR; `ir-migration.baseline.txt` and `introduced-functions.baseline.txt` shrink
- [x] 2.3 The conformance cases on `Intersect`, `Except`, `Average`, `Sum` and `GroupBy`, failing on main

## 3. Verbatim names and `using static`

- [x] 3.1 `TwinName.Of` drops the verbatim escape; a label and a type parameter go through `ToJsIdentifier`, and `DeclaredType` names a type parameter as its declaration does
- [x] 3.2 `UsingStaticSymbolExtensions.AsQualified` converts a bare platform member as its qualified spelling; proved by `UsingStaticFenceTests`
- [x] 3.3 The conformance cases on a label `package` and a member `@class` read and set in a `with`, failing on main

## 4. Proof and documentation

- [x] 4.1 The twins regenerated with `EQ_UPDATE_TRANSPILED=1`, the runtime type-checked and tested, the served runtime within its budget
- [x] 4.2 Run the five suites and build `samples/DefaultUIDashboard` with no warning
- [x] 4.3 The wiki's Compiler page, in English and Portuguese, on a wiki branch named like this pull request's
- [x] 4.4 One `docs/LEDGER.md` line citing #397, #467, #556 and #657
