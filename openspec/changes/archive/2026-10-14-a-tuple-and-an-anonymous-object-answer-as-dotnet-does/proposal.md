# Proposal

Closes #743 and #762, two Bugs under #565 (The transpiler's fences hold on every path, continued).

## Why

An anonymous object is a plain object in the browser and a value tuple an array, so neither has a
twin to carry an `equals` or a `toString`. At 85b0ab52, 63 of the 69 cases of
`TupleAndAnonymousObjectConformanceTests` fail:

- an anonymous object's `Equals` threw `equals is not a function`, as a call and as a method group
  (#743), and its text was `[object Object]`;
- a tuple's text was `1,2` where .NET writes `(1, 2)` (#762), in every route into text, a float in the
  double's digits and a number in the invariant culture on a pt-BR page;
- a tuple's `Equals` method group and a `Nullable` tuple's `Equals` threw.

The six that pass are what already answered as .NET: an anonymous object's `==`, the `GetHashCode` of
both, and a tuple's `Equals` call.

## What Changes

- An anonymous object's `Equals`, a call or a method group, compares by the comparison generated from
  its members' types (`$eq.collections.memberEquality`), which takes only another anonymous object of
  the same members in the same order: other members, the same members in another order, a record with
  the same members and a tuple are another type to .NET, unequal before a member is read.
- A tuple's `Equals` method group compares as its call does, and so does a `Nullable` tuple's `Equals`.
- A tuple writes `(1, a)` and an anonymous object `{ R = 1, Name = a }` and `{ }`, each element and member
  as its static type writes it in the culture in force, through every route a value takes into text:
  `ToString` and its method group, a hole (a format or an alignment too: neither type is `IFormattable`,
  so .NET ignores the format and pads the text), a concatenation, `Concat`, `Join`, `Format`, a builder,
  `Convert.ToString` and a record's or a tuple's member. Eight elements or more are written flat, as
  .NET writes the ones it nests in `Rest`.

A developer writes nothing new: the C# they already wrote answers in the browser what it answers on the
server.

## Impact

- eqc: `StringConversion` writes a tuple's and an anonymous object's text from the static type (and is
  what `ValueFlow` already calls for a value on its way into text), `InterpolatedStringStrategy` and
  `string.Format` hand the formatter that text, `MemberAccessStrategy` makes their `Equals` and
  `ToString` method groups, `StructuralEqualsStrategy` (now on the IR) and `ElementEquality` choose their
  `Equals`, and `ValueCopies` no longer copies a tuple boxed into text around the text it became.
- The runtime: `$eq.text.tuple` and `$eq.text.tupleGroup` (`utils/format.ts`), `$eq.text.record` with an
  empty name for an anonymous object, `$eq.equalsGroup` taking a comparison (`utils/equals.ts`), and
  `memberEquality`'s check of the other value's members (`utils/key-equality.ts`).
- The shared components' modules: `CodeLanguageRules` prints its `BlockComment` tuple as `(/*, */)`.
- Public surface: `Eq.TupleText` and `Eq.TupleTextGroup` are added, and `StructuralEqualsStrategy`'s
  `Convert` is replaced by `ConvertIr` as it crosses to the IR. The developer surface does not move.
- Not reached: a tuple or an anonymous object held as `object`, whose type the build cannot see, still
  prints as the browser holds it, `1,2` and `[object Object]`, and compares by its members in any order.
