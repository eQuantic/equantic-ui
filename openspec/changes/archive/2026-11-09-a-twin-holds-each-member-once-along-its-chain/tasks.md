## 1. Partial members

- [x] 1.1 Add `PartialMemberExtensions` (`src/eQuantic.UI.Compiler/CodeGen/Extensions/PartialMemberExtensions.cs`): the defining half of a partial property, indexer or event, and the members a twin is written from without it.
- [x] 1.2 Read the twin's members in every pass that writes or judges them: the class, static class, record, struct and component emitters, the type initializer's stores, a record's value members, the overload and one-key checks, and the component parser.

## 2. Static field slots

- [x] 2.1 Move a static field of any type eqc writes a twin of, a constant's included, past the static members of its type and its chain (`FieldSlotExtensions`), statics and instance members kept apart.
- [x] 2.2 Write every static store in its slot: a class's and a component's static fields, the type initializer's ordered stores, a record's constants and statics, and a bare static read (`IdentifierStrategy`).

## 3. Chain refusal

- [x] 3.1 Refuse with EQ1007 a member on the key a member of a base the source declares holds, statics included, where one of the two is not a method, naming the base's file and line (`OverloadedMethods.CheckInheritedKeys`).

## 4. Tests

- [x] 4.1 Compiler tests: `PartialMemberTests`, `StaticFieldSlotTests`, `InheritedKeyTests`, and the static pairs of `OverloadedMethodTests` and `ExplicitImplementationTests` moved from refused to accepted.
- [x] 4.2 A conformance test (`tests/eQuantic.UI.Conformance.Tests/MemberKeyConformanceTests.cs`): a partial property's setter on a class, a record and a static class, a partial indexer, and a static field behind a setter, beside a counter method, beside a constant, started by the type initializer, on a record and along a chain, both sides executed.

## 5. Documentation

- [x] 5.1 EQ1007 in `docs/DIAGNOSTICS.md` and the wiki's Diagnostics page, and a SupportedFeatures row for partial members, in every language the wiki is written in.
- [x] 5.2 A ledger entry under `docs/ledger/`.
