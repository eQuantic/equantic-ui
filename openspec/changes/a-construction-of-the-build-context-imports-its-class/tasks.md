# Tasks

## 1. Import by use

- [x] 1.1 The scanner imports the type's class for a value use and the annotation's name otherwise, decided by the bound tree. Verify: `AContextAModuleConstructs_ImportsItsClass_BesideTheInterfaceItsAnnotationsName`, which fails with main's scanner (the module imported `BuildContext` alone), and `TheBuildContext_IsTheRuntimesBuildContext_AndAHelperImportsIt` and `AnAppsOwnBuildContext_IsNotImportedFromTheRuntime` unchanged
- [x] 1.2 The runtime's `tsc` reads a class that annotates and constructs a context. Verify: `EmittedContexts` in the emission fixtures passes `tsc`, and with main's import it fails with `TS2304: Cannot find name 'ComponentContext'`

## 2. Proved against the real thing

- [ ] 2.1 The suites. Verify: Compiler, Web and Conformance pass, `tsc` is clean and the runtime's vitest passes
- [x] 2.2 A ledger entry under `docs/ledger/`. No wiki page describes how a module names the build context, so none changes
