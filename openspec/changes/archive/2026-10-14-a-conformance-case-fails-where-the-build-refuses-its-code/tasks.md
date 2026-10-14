# Tasks

## 1. The rule

- [x] 1.1 `Refusal` reads a converter's diagnostics and a compiled module's errors by the build's own rule: an `Error` refuses and a warning does not, a module refuses with every error when it did not succeed, an error with no id is `EQ0000`
- [x] 1.2 Every entry of `Transpiler` answers a `Translation`, whose text the runner reaches through `Accepted()` only, and a refused case fails with `RefusedByTheBuildException`, naming every refusal by its id and text
- [x] 1.3 The build's own verdict on the prelude's declarations, asked of `ComponentCompiler` once per prelude by one compiler under a lock
- [x] 1.4 `ModuleGraph` and the four places that call `ComponentCompiler` themselves read every module's errors before they keep the modules that wrote text

## 2. A case that pins a refusal

- [x] 2.1 `ConformanceRunner.AssertStatementsRefusedByTheBuild` and `AssertRefusedByTheBuild`: C# that compiles, a refusal of the case's own C# by the id, no refusal of another id (its prelude's included), nothing run
- [x] 2.2 `Transpiler.TranslateStatements` and its siblings, for a case that pins the text of a refused construct
- [x] 2.3 The harness checked against itself: a refused case fails naming its id, a warning refuses nothing, a prelude's EQ1007 and a module's EQ1007 fail their case, and an expected refusal fails when the build accepts the case's own C# (its prelude's refusal not standing in for it), refuses it or its prelude for another reason, or the C# does not compile, and passes beside a prelude refused with the same id
- [x] 2.4 #761's three queries pinned as EQ2008

## 3. The suite with the rule

- [x] 3.1 The whole conformance suite with the rule, and every case it refuses sorted: rewritten in C# the build accepts, pinned as the refusal it documents, or pinned until the refusal it reveals is fixed under an issue of its own
- [x] 3.2 The other test projects read for code they run in a JavaScript engine: none runs the converter's text, and the builds they run fail on the build's errors already
- [x] 3.3 A/B: main's harness runs the refused cases and passes them or reports a wrong answer, and this one fails them naming the refusal and passes the cases that declare it

## 4. Documentation

- [x] 4.1 `docs/DOTNET-COVERAGE-PROGRAM.md`, the harness's methodology
- [x] 4.2 The wiki's SupportedFeatures page, in its five languages, on the branch named like this one
- [x] 4.3 The event, as a file under `docs/ledger/`
- [x] 4.4 Archive the change with `openspec archive a-conformance-case-fails-where-the-build-refuses-its-code --yes`
