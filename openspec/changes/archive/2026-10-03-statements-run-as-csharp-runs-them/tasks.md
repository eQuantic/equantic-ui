# Tasks

## 1. Patterns

- [x] 1.1 Test a type pattern as a declaration pattern, by the symbol. Verify: `PatternConformanceTests.ABareTypePattern_TestsTheType` runs both sides, and fails against main
- [x] 1.2 Compare a named constant in `is` with its value. Verify: `PatternConformanceTests.IsANamedConstant_ComparesToItsValue`, failing against main

## 2. Statements

- [x] 2.1 Run a lock's expression and make `new object()` a fresh object. Verify: `ALocksExpression_Runs` and `ANewObject_IsAValueOfItsOwn`, failing against main
- [x] 2.2 Hoist a captured loop variable and call a delegate value as its expression. Verify: `ALoopsVariable_IsOnePerLoop_AndADelegateIsCalledAsItsValue`, failing against main

## 3. Records and statics

- [x] 3.1 Deconstruct a record by assignment. Verify: `DeconstructionConformanceTests.ARecordDeconstructedByAssignment_FillsItsTargets`, failing against main
- [x] 3.2 Keep a static `field` store on the type in the three emitters. Verify: `AStaticFieldBackedProperty_KeepsItsStoreOnTheType` runs a record on both sides, and the compiler pins the component's and the record's slots, all failing against main

## 4. Documentation and the suites

- [x] 4.1 The wiki's SupportedFeatures page (EN + pt-BR) on a branch named like this one. Verify: the wiki guards pass with `EQ_WIKI_DIR` on it
- [ ] 4.2 The suites, each alone and read by its exit code. Verify: Compiler, Web, Server, Conformance and the runtime's `TestRuntime` exit 0
