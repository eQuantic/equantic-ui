# Tasks

## 1. Every module starts from a reset converter

- [x] 1.1 `TypeScriptEmitter.BeginModule` resets the converter, then sets the annotations, the model and the class; `Emit` and `EmitClassModule` start with it. Verify: `CompilerReuseTests.AClassModule_NumbersItsOwnTemporaries_WhateverWasCompiledBeforeIt`, for a plain class and a static helper, fails against `main` (`$n2` where `$n0` is expected) and passes
- [x] 1.2 `RecordTypeEmitter.EmitModule` resets the converter it is handed. Verify: `CompilerReuseTests.ARecordModule_NumbersItsOwnTemporaries_ThroughAConverterItShares` fails against `main` and passes

## 2. Proof against the real thing

- [x] 2.1 The runtime's twins regenerated (`SharedComponentTranspilationTests`, `EQ_UPDATE_TRANSPILED=1`), and every changed twin differs from `main` by the names of its temporaries alone, through a one-to-one renaming
- [x] 2.2 The runtime type-checks and its tests pass (`dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`), and the Compiler, Web, Conformance and Server suites pass, the served runtime's budget included

## 3. Documentation

- [x] 3.1 A ledger entry under `docs/ledger/` citing #1138
