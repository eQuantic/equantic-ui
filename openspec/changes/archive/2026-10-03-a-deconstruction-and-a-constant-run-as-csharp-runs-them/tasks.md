# Tasks

## 1. Deconstruction

- [x] 1.1 One lowering for the declaration, the assignment and the foreach, from the bound tree. Verify: `DeconstructionConformanceTests.EveryDeconstruction_ReadsARecordOrAStructByItsDeconstruct` runs both sides, its non-control cases failing against the branch's first cut

## 2. Constants and types in patterns

- [x] 2.1 A long constant as a BigInt, and one constant test for both spellings. Verify: `PatternConformanceTests.AConstantInAPattern_IsTestedByItsValue` and `StatementConformanceTests.ALongConstant_IsALong`, failing against the first cut
- [x] 2.2 A char as one code unit. Verify: the `char` arms of `AConstantInAPattern_IsTestedByItsValue`

## 3. Stores, loops and local functions

- [x] 3.1 A static store's first value in a component and a class. Verify: `CSharpVersionCoverageTests.AStaticStore_StartsAsItsInitializerOrItsTypesDefault`, failing against the first cut
- [x] 3.2 A hoisted loop's initializer variables, and a local function's outs. Verify: `AnInitializersOutVariable_IsOnePerLoop_WhenTheLoopIsHoisted` and `ALocalFunctionsOut_ReachesItsCaller`, failing against the first cut

## 4. Documentation and the suites

- [x] 4.1 The wiki's SupportedFeatures page (EN + pt-BR) on the branch named like this one
- [x] 4.2 The suites, each alone and read by its exit code: Compiler, Web, Server and Conformance
