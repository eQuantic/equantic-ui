# Tasks

## 1. One name for a twin

- [ ] 1.1 A type's twin name, from its symbol and from its declaration: the chain of its containing types and its own name joined by `$`, generic arguments erased. Verified by Compiler tests for a top-level, a nested, a doubly nested and a generic owner's type
- [ ] 1.2 Every strategy and emitter that writes a type into code names its twin: a construction, a type test, a static member, an operator, a conversion, a zero, a `with`, an annotation, a hydration map and a class declaration. Verified by the conformance cases of group 2 and the Compiler suite

## 2. A nested type is a module

- [ ] 2.1 The parser and the dependency resolver take a nested class, static class, record and struct as a module named by its twin, and none for one whose owner never crosses; a component's nested static classes are modules like any other. Verified by conformance cases through the module graph (the two of #584, a nested record beside a top-level one, a nested static class, a public nested type from outside, two levels), failing before and green here, and a Compiler test for the server-only owner

## 3. The runtime and the real thing

- [ ] 3.1 Regenerate the runtime's transpiled classes (`CodeBlock$CodeMetrics`) and run `dotnet build src/eQuantic.UI.Runtime -t:TestRuntime`, the Server suite and the Web suite
- [ ] 3.2 The dashboard sample in a browser: the code screens, whose `CodeBlock` builds a nested record, render with no console error

## 4. Documentation and archive

- [ ] 4.1 The wiki's SupportedFeatures page in English and Portuguese, on the wiki branch named like this change's branch, and one docs/LEDGER.md line citing #584
- [ ] 4.2 `./scripts/check-openspec.sh` green, then `openspec archive a-nested-class-is-its-owners --yes` before the merge
