## 1. Lowering

- [x] 1.1 Add `ExplicitImplementations` (`src/eQuantic.UI.Compiler/CodeGen/ExplicitImplementations.cs`): what a member hands off to (its parameters passed through, a property's setter included), which explicit implementations a type writes under a name, and which it leaves out.
- [x] 1.2 Ask it from the class, record and component emitters, and from `TwinMethodName.ExplicitBeside`, so a method an explicit implementation hands off to keeps its name; have `IterableTwin.AnswersAs` read hand-offs through it.
- [x] 1.3 Keep a component's explicit implementation beside a method of its name in `ComponentParser`.

## 2. Refusal

- [x] 2.1 Refuse with EQ1007 two explicit implementations a type writes under one name, one beside a property, an event or an override of the type's own on its name, and one on the name of a member a base the source declares holds, the base's explicit implementation of the same interface member excepted (`OverloadedMethods.CheckExplicitImplementations`).
- [x] 2.2 Rewrite the overload check's exemption of explicit implementations to say where they are judged.

## 3. Tests

- [x] 3.1 Compiler tests (`tests/eQuantic.UI.Compiler.Tests/ExplicitImplementationTests.cs`): each hand-off left out, each case refused with its message and location, a re-implementation accepted, and a component's pair written.
- [x] 3.2 A conformance test (`tests/eQuantic.UI.Conformance.Tests/ExplicitImplementationConformanceTests.cs`): calls through each interface, through a constrained type parameter and through the class, over an implicit and explicit pair, two interfaces of one name, explicit properties and a base's method, both sides executed.

## 4. Documentation

- [x] 4.1 EQ1007 in `docs/DIAGNOSTICS.md` and the wiki's Diagnostics page, and a SupportedFeatures row, in every language the wiki is written in.
- [x] 4.2 A ledger entry under `docs/ledger/`.
