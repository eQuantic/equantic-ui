# Tasks

## 1. An exception carries its type

- [x] 1.1 The runtime's `utils/exceptions.ts`: an Error with its chain, the test a catch writes, `raise` and `filter`, and every .NET throw of the twins under `utils/` typed as .NET measures it, a spec failing on a new untyped one. Verify: `RuntimeExceptionHierarchyTests` compares the runtime's table with .NET's own types, and `CatchClauseConformanceTests.AnExceptionTheRuntimeThrows_IsOfTheTypeDotNetThrows`, failing against main
- [x] 1.2 `new T(…)` for an exception type by its symbol (`ExceptionCreationStrategy`), and the type test of `PatternConverter` over an exception. Verify: `AnExceptionOfTheAppsOwn_IsCaughtByItsType` and `ATypePatternOverAnException_TestsItsType`, failing against main

## 2. One catch that tries the clauses

- [x] 2.1 The clauses as one catch, each by its type and its filter, the filter's variables declared, a throwing filter false, what none takes thrown on, and `throw;` rethrowing the exception caught; the try IR holds one catch. Verify: `CatchClauseConformanceTests.ACatchClause_TestsItsTypeAndItsFilter`, the issue's four rows among them, failing against main

## 3. No function of the transpiler's own around C#

- [x] 3.1 `checked` and `unchecked` as their operand, a throw expression through `$eq.exceptions.raise`, `Trim`'s characters, and `Range`'s and `Repeat`'s arguments as arguments. Verify: `IntroducedFunctionConformanceTests`, every row of the issue's table with an awaited argument and with a counted one, failing against main
- [x] 3.2 The functions the lowerings still write by hand, counted per file against a baseline that may only shrink. Verify: `IntroducedFunctionsCoverageTests`, measured failing on a probe that wrote `Range`'s old callback back

## 4. Documentation and the suites

- [x] 4.1 The transpiled library's pins regenerated and read: the components that throw or trim
- [x] 4.2 The wiki's SupportedFeatures page (EN + pt-BR) on the branch named like this one, and one `docs/LEDGER.md` line
- [x] 4.3 The suites, each alone and read by its exit code: Compiler, Web, Server, Conformance and the runtime's `TestRuntime`
