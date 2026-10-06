# Tasks

## 1. Which types have a twin or a module

- [x] 1.1 One predicate for a plain class's module, read by the parser and the resolver (#423). Verify: `PlainClassModuleTests` fails when the two disagree, and `ClassModuleConformanceTests` runs the module graph on both sides, failing against main
- [x] 1.2 A twin for every record and struct whatever it declares (#428). Verify: `RecordTwinConformanceTests`, failing against main

## 2. Construction

- [x] 2.1 The twin's constructor is the C# constructor: its parameters, every initializer in declaration order, a derived record's before its base's constructor, then the body; a `with` copies (#413). Verify: `RecordConstructionConformanceTests.ARecordOrAStruct_IsBuiltAsCSharpBuildsIt`, failing against main
- [x] 2.2 Alternate constructors reached by their counts of arguments, EQ1009 for the rest. Verify: `TwinConstructorTests` and the alternate cases of `RecordConstructionConformanceTests`, failing against main
- [x] 2.3 An object initializer applied after the constructor, a nested collection initializer through `Add`, a nested object initializer into the member's object, an entry through the indexer (#462). Verify: `NestedInitializerConformanceTests`, records and classes, failing against main
- [x] 2.4 A struct's zero runs nothing. Verify: `StructDefaultTests` and the struct cases of `RecordConstructionConformanceTests`

## 3. A record's value

- [x] 3.1 One member for a positional parameter whose property the record declares (#546). Verify: `RecordConstructionConformanceTests.ARecordsMembers_AreEachOnce_AndItsTextIsDotNets`, failing against main
- [x] 3.2 Equality by runtime type and every field, text as `PrintMembers` writes it. Verify: the same theory's equality and text cases

## 4. Indexers and statics

- [x] 4.1 An instance indexer as `item` and `setItem`, every bound access calling them, a default indexer included, EQ1007 for its names (#427). Verify: `IndexerConformanceTests`, records and classes, failing against main
- [x] 4.2 A type initializer: zeros, initializers in declaration order, then the static constructor, on first use, in the three emitters (#417). Verify: `StaticInitializationConformanceTests`, records, classes, a static class and a component, failing against main

## 5. Review

- [x] 5.1 An initializer's parts are arguments of the function that applies it, and a dictionary's pair is its `Add`. Verify: `NestedInitializerConformanceTests.AnInitializersParts_RunInTheCallersFunction` and `APairAddedToAMembersDictionary_IsItsAdd`, failing against the previous lowering
- [x] 5.2 An exception or an attribute keeps a class out by its chain of bases. Verify: `PlainClassModuleTests` with a chain of three, failing against the name-based rule
- [x] 5.3 Every constructor with a body of its own is a branch. Verify: the `Money`, `Rect` and `Early` cases of `RecordConstructionConformanceTests`, failing against the previous emitter
- [x] 5.4 A static constructor runs before the first instance and the first use of any static member. Verify: the trigger cases of `StaticInitializationConformanceTests`, records, classes and a component, failing against the data-access-only initializer

## 6. Documentation and the suites

- [x] 6.1 The shared library's pins regenerated and read, and the runtime's specs built through `Object.assign`. Verify: `SharedComponentTranspilationTests` and `TestRuntime`
- [x] 6.2 EQ1009 in docs/DIAGNOSTICS.md, EQ1007 and EQ1008 updated, one docs/LEDGER.md line citing the issues. Verify: `DiagnosticsDocumentedTests`
- [x] 6.3 The wiki's SupportedFeatures and Diagnostics pages (EN + pt-BR) on a branch named like this one. Verify: the wiki guards pass with `EQ_WIKI_DIR` on it
- [x] 6.4 The suites, each alone and read by its exit code, and the dashboard sample built. Verify: Compiler, Web, Server, Conformance and the runtime's `TestRuntime` exit 0
