## 1. The SDK

- [x] 1.1 Write `equantic.sources.txt` from the app's `Compile` items and `DefineConstants`, after `AddImplicitDefineConstants` and `GenerateGlobalUsings`.
- [x] 1.2 Ask each project eqc compiles for its section through `Sdk/EQuanticProjectSources.targets`, with the ProjectReference protocol's global properties, and hand the list with `--sources`.

## 2. eqc

- [x] 2.1 Read the list (`ProjectSources.ReadAll`), refusing one that does not hold (EQ0001).
- [x] 2.2 Compile the app and each referenced project from its list, with its symbols, and transpile and scan the app's list in place of its directory.
- [x] 2.3 Parse a file for transpilation with the options of the tree it replaces (`SemanticModelProvider.OptionsFor`).

## 3. Tests

- [x] 3.1 `WhatCSharpCompiledTests`: the app's and a library's symbols pick their branches, a removed file is no module and a linked file is one, the generated usings are the configuration's alone, and with no list the directory is walked; the test solution hands each project's list, so every referenced-project test runs on it.
- [x] 3.2 Measured on `samples/SharedLibrary`: the list holds the app and its three projects, and an `#if DEBUG` in the app and in its library answers its configuration's branch in Debug and in Release.

## 4. Documentation

- [x] 4.1 The wiki's BuildFlow page, in all five languages.
- [x] 4.2 A ledger entry under `docs/ledger/`.
