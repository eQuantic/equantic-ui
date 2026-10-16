# Tasks

## 1. A verbatim name (#704)

- [x] 1.1 Read every identifier eqc takes from syntax by its value: `Identifier.ValueText` across the SDK's source, and `ValueText()` for a type's syntax in the compiler
- [x] 1.2 Refuse both spellings in `NoNameReadAsSpelledTests`, the second asked of the compiler's own source compiled with a model, which fails when that source does not compile
- [x] 1.3 Pin a type, a record, an enum, a namespace, an exception, a base class, members, parameters, locals, a pattern, a cast, an array and a list spelled verbatim in `VerbatimNameConformanceTests`, executed on both sides through the module graph, in TypeScript and in JavaScript

## 2. A member a derived class declares (#740)

- [x] 2.1 Move a field when a type of its own source derived from its class declares a property, an event or a method a case apart from it
- [x] 2.2 Refuse with EQ1007 a member that lands on the slot of a field a base of another project holds
- [x] 2.3 Pin 99001 and its generic and two-level cases in `MemberCaseConformanceTests`, and the refusal and its control in `ReferencedProjectRefusalTests`

## 3. A lambda over a long or a decimal (#758)

- [x] 3.1 Annotate `bigint` and `Decimal`, the latter imported
- [x] 3.2 Add a long and a decimal lambda to the `BoundReceivers` fixture and run the runtime's `tsc` over it: three errors before, none after

## 4. A component the generated file cannot reach (#703)

- [x] 4.1 Write a factory only for a component that, with every class around it, is public and not server-only
- [x] 4.2 Compile the generated surface in `AppFactorySurfaceGeneratorTests` for each owner that hid it, and its public control

## 5. Proof and documentation

- [x] 5.1 Run the compiler, conformance, Web, Server and Design suites, and the runtime's suite; the transpiled pins are unchanged but for the fixture
- [x] 5.2 The wiki: SupportedFeatures and DeclarativeSurface in the five languages, Diagnostics in English and Portuguese, on the wiki branch of the same name
- [x] 5.3 One ledger entry under `docs/ledger/` citing the issues
