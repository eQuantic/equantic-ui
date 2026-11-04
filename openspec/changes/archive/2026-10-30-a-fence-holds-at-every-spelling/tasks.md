## 1. Known by the symbol

- [x] 1.1 `IsServerAction` asks the symbol, with `PlainClassModule.ServerActionMark` as the one reading, and `ParseServerActions` reads the attribute it found, an alias's `Name` included. Verify: `ServerActionBySymbolTests`, in an app build and compiled on its own, fail on main for the alias and the partial method

## 2. The fences

- [x] 2.1 `TwinOwnMembers.RefuseKeptOnTheServer`, asked by the class, component and record emitters. Verify: `AServerOnlyOverrideTests` fail on main
- [x] 2.2 `PatternConverter.TypeCheck` refuses a package's type. Verify: `ATypeTestOfAPackagesType_FailsTheBuild_NamingTheTypeAndTheAssembly` fails on main
- [x] 2.3 A keyword on the left of a member reports nothing of its own. Verify: `ARefusedStaticCall_IsToldOnce_WhicheverSpellingNamesTheType` fails on main, and the BCL baseline moves ten verdicts from EQ1001 to EQ2004 and nothing else

## 3. Against the real thing

- [x] 3.1 The whole Compiler and Conformance suites, with both sides executed

## 4. Documentation

- [x] 4.1 The wiki's Diagnostics page in all five languages
- [x] 4.2 One entry for the ledger citing the four issues, as a file of its own under `docs/ledger/`
