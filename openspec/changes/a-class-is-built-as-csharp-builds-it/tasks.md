# Tasks

## 1. One constructor builder

- [ ] 1.1 Move the record twin's constructor (roots, alternates, arity tests, bindings, chain arguments, base call, bodies) out of `RecordTypeEmitter` into one builder that returns the constructor as IR; the record emitter writes it in its compact layout. Verified by the Compiler suite, and by every moved transpiled pin being identical to the old one with whitespace stripped before it is regenerated
- [ ] 1.2 The conformance suite run on the record twins unchanged (`RecordConstruction`, `RecordTwin`, `StructZero`, `StaticInitialization`, `ValueType`), every case green

## 2. A class built as C# builds it

- [ ] 2.1 The plain class path builds its constructor with the builder: its instance fields, auto-properties, `field` stores, events and held primary parameters started in declaration order, declared for TypeScript only. Verified by conformance cases through the module graph for #571 and #582's declaration order, failing on #608's head and green here
- [ ] 2.2 A class's constructors, `: this(…)` chains, `: base(…)` arguments, primary constructor and base clause reached as a record's are, EQ1009 for a count two constructors share. Verified by conformance cases for #583 (chain, base arguments, primary constructor, base clause) and a Compiler test for EQ1009 on a class
- [ ] 2.3 EQ1007 for a class's held primary parameter on another member's name, and none for a parameter read only by an initializer. Verified by Compiler tests for both
- [ ] 2.4 `new C(…) { … }` over a plain class whose twin eqc writes builds, then applies the initializer; a component and a vocabulary twin keep their config. Verified by #582's conformance cases (the values after the constructor, the log's order) and the existing component and vocabulary pins unchanged

## 3. An exception's initializer

- [ ] 3.1 An exception built with an object initializer has it applied. Verified by conformance cases for a field, a property and a nested collection initializer on an app exception, failing on #608's head and green here

## 4. The runtime and the real thing

- [ ] 4.1 Regenerate the runtime's transpiled classes (`EQ_UPDATE_TRANSPILED=1`) and run `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, the Server suite (the served runtime's budget) and the Web suite
- [ ] 4.2 The dashboard sample in a browser: every screen by client navigation with no console error, the code editor, the form, the sheet and the markdown screen exercised, since their models are transpiled classes

## 5. Documentation and archive

- [ ] 5.1 The wiki's SupportedFeatures and Compiler pages, and Diagnostics for EQ1007 and EQ1009 on a class, in English and Portuguese, on the wiki branch named like this change's branch; the base-constructor difference documented on SupportedFeatures
- [ ] 5.2 docs/DIAGNOSTICS.md for EQ1007 and EQ1009 on a class, and one docs/LEDGER.md line citing #571, #582, #583 and #587
- [ ] 5.3 `./scripts/check-openspec.sh` green, then `openspec archive a-class-is-built-as-csharp-builds-it --yes` before the merge
