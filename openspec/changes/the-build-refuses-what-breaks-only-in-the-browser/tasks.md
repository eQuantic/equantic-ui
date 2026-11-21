# Tasks

## 1. Measure the pitfalls against main

- [x] 1.1 Compile each one with eqc and the vocabulary referenced: `string.Format` translates, an overload is EQ1007, a `long` quotient converts into a vocabulary float; an `Icon` of 22, a static read from a catalog and a type named like the SDK's pass the build
- [x] 1.2 Render a page that loads and hands the value to a stateful section, through the tester and the server: the section shows the default

## 2. The analyzers

- [x] 2.1 `ResourceShape` in `src/Shared`, read by eqc's `ResourceClasses` and by the analyzer
- [x] 2.2 `AllowedValueAnalyzer` (EQ3106) and `CultureFrozenResourceAnalyzer` (EQ3107), linked into both generators
- [x] 2.3 `Icon` and `UI.Icon` mark `size` with `[AllowedValues]` over `IconSize`; `UiFactoryConformanceTests` holds a factory to its constructor's mark, and the app's generated factories copy the mark, its values written as literals
- [x] 2.4 Verify: analyzer tests both ways (a naive `Equals` fails the ints-on-a-double case, an unmarked factory fails the factory cases, a lambda counted as read once and any instance field counted each fail one case); the native generator ships both; a real build of the dashboard sample with a probe file reports EQ3106 and EQ3107

## 3. The module's names

- [x] 3.1 `NamedSourceTypes` records the runtime's types and the written ones; `ComponentCompiler` refuses a name held by one of each (EQ1005), behind `DeclaresATypeNamedLikeTheRuntimes`
- [x] 3.2 Two types of one name in one file in different namespaces are EQ1005, naming both
- [x] 3.3 Verify: without the check three cases fail, counting server code fails one; `AppBuild` over a solution on disk stops at the page; the dashboard sample with a probe file reports EQ1005, and builds clean without it

## 4. The server's rounds

- [x] 4.1 `BuildOwner.KeepsInstance`, read by `ComponentElement.Update`; `ServerDataRounds.RunAsync` arms it with "is it a loader" and puts it back
- [x] 4.2 Regenerate the engine's committed twins (`EQ_UPDATE_TRANSPILED=1`)
- [x] 4.3 Verify: the tester and `ServerRenderingService` draw the section with the loaded value; main fails both, and keeping no instance (loaders included) asks a stateful loader twice

## 5. Documentation

- [x] 5.1 `docs/DIAGNOSTICS.md` rows for EQ1005, EQ3106 and EQ3107, and the diagnostics baseline
- [x] 5.2 The wiki's Diagnostics page in its five languages, on the branch named like this one
- [x] 5.3 One entry for the ledger citing #1776 and #1777, as a file of its own under `docs/ledger/`
