## 1. Runtime types

- [x] 1.1 Add `utils/runtime-types.ts` (`of`, `named`, `same`) over the closure marks, moved from `eq.ts` to `utils/closures.ts`, and `exceptions.typesOf`, exported as `$eq.types`, with a vitest spec.
- [x] 1.2 Extend `ObjectCreationStrategy.ClosureOf` to a generic class the source declares.
- [x] 1.3 Make the rule for a type with a class of its own one internal predicate of `PatternConverter` (`IsAJsClass`), read by `TypeCheck` and by the comparison, which adds the vocabulary's roots as receivers.
- [x] 1.4 Add `RuntimeTypeEqualityStrategy`: `==` and `!=` between `GetType()` and `GetType()` or `typeof`, refusing a receiver or a `typeof` that does not qualify (EQ2004).

## 2. Guards

- [x] 2.1 Add `actualText` to `ExceptionParts`, written in the message in place of the value's invariant text.
- [x] 2.2 Add `utils/guards.ts` with the twelve guards, .NET's checks and words, exported as `$eq.guard`, with a vitest spec.
- [x] 2.3 Add `ArgumentGuardStrategy`: the bound method, the template over the written arguments, the parameter's name from the bound tree, the equality, the ordering and the text arrow, refusing a type the browser does not hold or order faithfully (EQ2004).

## 3. Proof

- [x] 3.1 A conformance test (`tests/eQuantic.UI.Conformance.Tests/ArgumentGuardConformanceTests.cs`): every guard throwing and passing, in the invariant culture and in pt-BR, NaN, negative zero, nulls, a string's order, a missing and an empty parameter name, arguments named out of order, `ParamName`, `ActualValue` and the message, both sides executed.
- [x] 3.2 A conformance test (`tests/eQuantic.UI.Conformance.Tests/RuntimeTypeConformanceTests.cs`): classes, a base against a derived class, records, structs, generic classes and records under two closed types and one built in generic code, exceptions, `typeof` on either side, `!=`, a null receiver and the order of evaluation, both sides executed.
- [x] 3.3 Refusal cases in both conformance classes, through the build: a numeric, `object`, an array, an interface, a string and an unconstrained type parameter receiver, a `typeof` that does not qualify, `GetType().Name`, and a guard over `nint` and over a `Guid`.
- [x] 3.4 The element engine's `Element.CanUpdate` and `NodeElement`'s guard build through eqc with no diagnostic (the E3a probe, run against the engine's files with `[ServerOnly]` lifted, since a server-only type compiles to nothing and the probe would pass having read it: `Element` builds, and what `NodeElement` still meets is its message's `GetType().Name`, which E3a rewrites. Measured again on 2026-11-09 on this branch merged with main; the engine's own pin, which E3a adds, is what keeps it).

## 4. Documentation

- [x] 4.1 A ledger entry under `docs/ledger/`, citing #1202.
- [x] 4.2 The wiki's `SupportedFeatures` (the `object` members row, a row for the argument guards) and `Diagnostics` (EQ2004), in all five languages, on the wiki branch of the same name.
