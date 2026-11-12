# Tasks

## 1. Import by use

- [x] 1.1 The scanner imports the type's class for a value use and the annotation's name otherwise, decided by the bound tree. Verify: `AContextAModuleConstructs_ImportsItsClass_BesideTheInterfaceItsAnnotationsName`, which fails with main's scanner (the module imported `BuildContext` alone), and `TheBuildContext_IsTheRuntimesBuildContext_AndAHelperImportsIt` and `AnAppsOwnBuildContext_IsNotImportedFromTheRuntime` unchanged
- [x] 1.2 A construction through a constructor the compilation cannot reach, which binds an `IInvalidOperation`, is a construction too: the element engine's, through `ComponentContext`'s internal constructor, seen from outside the vocabulary's assembly. Verify: `AContextBuiltThroughAConstructorOutOfReach_ImportsItsClass`, which fails when only an `IObjectCreationOperation` counts
- [x] 1.3 The runtime's `tsc` reads a class that annotates and constructs a context. Verify: `EmittedContexts` in the emission fixtures passes `tsc`, and with main's import it fails with `TS2304: Cannot find name 'ComponentContext'`

## 2. Proved against the real thing

- [x] 2.1 The suites. Verify: on the first head Web 1427, Compiler 2910, Conformance 7818/7819, `tsc` clean and vitest 2986/2986; on the final head, after the two scanner fixes, Compiler 2911, `tsc` clean and Web 1426/1427, the one being the wiki's Diagnostics guard reading a wiki master that already carries main's newer EQ2116
- [x] 2.2 A ledger entry under `docs/ledger/`. No wiki page describes how a module names the build context, so none changes
