# Tasks

## 1. A framework exception's message is composed as .NET composes it

- [x] 1.1 Measure .NET 10's messages: each type's own text, a null message, the parameter's name, the actual value, a disposed object's name, and `ParamName`
- [x] 1.2 eqc: the message by its parameter for a framework constructor, and the parameter name, the actual value, the inner exception and the object name by theirs
- [x] 1.3 The runtime: `create` composes the message and carries the parts as members
- [x] 1.4 Prove it on both sides in `ExceptionMessageConformanceTests`, failing on the base, the two-line messages compared except for the host's newline
- [x] 1.5 The review's first round: the text read from .NET for each constructor, a type initializer's null name and its `TypeName`, holes of any number of digits, and the aggregate a cancellation throws, each proved on both sides

## 2. Documentation and the suites

- [x] 2.1 The wiki's SupportedFeatures page, English and Portuguese, on the wiki branch of this pull request
- [x] 2.2 `docs/LEDGER.md`: one line citing #558
- [x] 2.3 The full suites, each alone and read by its exit code: Compiler, Conformance, Web, Server, and the runtime's `TestRuntime`
