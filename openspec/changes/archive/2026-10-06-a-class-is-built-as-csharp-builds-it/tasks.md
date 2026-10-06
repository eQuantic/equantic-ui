# Tasks

## 1. One constructor builder

- [x] 1.1 Move the record twin's constructor (roots, alternates, arity tests, bindings, chain arguments, base call, bodies) out of `RecordTypeEmitter` into one builder that returns the constructor as IR; the record emitter writes it in its compact layout. Verified by the Compiler suite, and by every moved transpiled pin being identical to the old one with whitespace stripped before it is regenerated
- [x] 1.2 The conformance suite run on the record twins unchanged (`RecordConstruction`, `RecordTwin`, `StructZero`, `StaticInitialization`, `ValueType`), every case green

## 2. A class built as C# builds it

- [x] 2.1 The plain class path builds its constructor with the builder: its instance fields, auto-properties, `field` stores, events and held primary parameters started in declaration order, each declared as a class field with no initializer. Verified by conformance cases through the module graph for #571 and #582's declaration order, failing on #608's head and green here
- [x] 2.2 A class's constructors, `: this(…)` chains, `: base(…)` arguments, primary constructor and base clause reached as a record's are, EQ1009 for a count two constructors share. Verified by conformance cases for #583 (chain, base arguments, primary constructor, base clause) and a Compiler test for EQ1009 on a class
- [x] 2.3 EQ1007 for a class's held primary parameter on another member's name, and none for a parameter read only by an initializer. Verified by Compiler tests for both
- [x] 2.4 `new C(…) { … }` over a plain class whose twin eqc writes builds, then applies the initializer; a component and a vocabulary twin keep their config. Verified by #582's conformance cases (the values after the constructor, the log's order), and by the regenerated twins: no component's own constructor moves, two components only where they build a plain class

## 3. An exception's initializer

- [x] 3.1 An exception built with an object initializer has it applied. Verified by conformance cases for a field and a property an initializer sets on an app exception, failing on #608's head and green here (its own members are #611)

## 3b. A transpiled vocabulary type

- [x] 3b.1 `[TwinIsTranspiled]` on every class, record and struct of the vocabulary's transpiled folders, held by a test that no other type carries it, and read by the construction, the zero, the indexer and the type test (#592). Verified by conformance cases through the module graph for `CellRef`, `FieldError`, `SheetEdit` and `SheetController` built with an initializer, a `with` and a zero, 3 of 6 failing on #608's head and 5 of 6 on this branch without the mark

## 4. The runtime and the real thing

- [x] 4.1 Regenerate the runtime's transpiled classes (`EQ_UPDATE_TRANSPILED=1`) and run `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, the Server suite (the served runtime's budget) and the Web suite
- [x] 4.2 The dashboard sample in a browser: every screen by client navigation with no console error, the code editor, the form, the sheet and the markdown screen exercised, since their models are transpiled classes

## 5. Documentation and archive

- [x] 5.1 The wiki's SupportedFeatures and Compiler pages, and Diagnostics for EQ1007 and EQ1009 on a class, in English and Portuguese, on the wiki branch named like this change's branch; the base-constructor difference documented on SupportedFeatures
- [x] 5.2 docs/DIAGNOSTICS.md for EQ1007 and EQ1009 on a class, and one docs/LEDGER.md line citing #571, #582, #583 and #587
- [x] 5.3 `./scripts/check-openspec.sh` green, then `openspec archive a-class-is-built-as-csharp-builds-it --yes` before the merge
