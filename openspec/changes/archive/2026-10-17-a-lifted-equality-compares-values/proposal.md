# Proposal

Closes #948, a Bug under #849 (The transpiler's fences hold on every path, part three).

## Why

C# LIFTS a struct's operator applied over nullable values: an equality finds two absent values equal
and an absent value unequal to any value, a relation is false where a value is absent, any other
operator answers null, and the struct's own operator runs only over values. The browser did none of
it. Two nullable record structs compared with `==` or `!=` were `===` and `!==`, so two equal values
built apart were unequal: #948 met it in the spreadsheet's engine, where `FillTarget != before` over
two `SheetRange?` answered true for the same cell. A struct's own operator over a nullable value was
handed the null and threw reading a member of it, and a conversion between two nullable types
converted the null. Measured with 67 statement cases and 9 module-graph cases executed on both sides:
28 and 3 of them failed before the change.

## What Changes

For a developer, a struct's operator over nullable values answers in the browser what it answers in .NET:

- `a == b` and `a != b` over two nullable record structs, or a nullable one beside a value, compare by
  value, member by member, and two absent values are equal; the same holds for a vocabulary value the
  browser holds as data, and inside a lambda, a LINQ predicate, a query and a `when` guard.
- A struct's own `==` and `!=` over nullable values answer for an absent value without the operator,
  and call it over two values; `<`, `>`, `<=` and `>=` are false where a value is absent; `+` and every
  other binary operator, a unary operator, a compound assignment and a step answer null for an absent
  value. An element of a nullable tuple compares through the same lift.
- A user-defined conversion from a nullable value to a nullable value converts only a value, null
  converting to null, wherever C# applies it: a declaration, a return and a cast among them.
- An operator declared over the nullable values themselves, `operator ==(Probe? a, Probe? b)`, is called
  as it is written, a null included, as C# calls it.

Nothing breaks: an app that met the old answers met wrong ones, or a TypeError.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-expressions`: a struct's operator over nullable values is lifted as C# lifts it.

## Impact

- eqc: `BinaryExpressionStrategy` reads the bound operator once and hands its lift to the one lowering of
  a user-defined operator, and a lifted equality whose operator no twin carries, a record struct's or a
  vocabulary value's, takes `$eq.equals`. `UserDefinedOperators.Binary`, `Unary` and `Conversion` take
  the operands as IR and whether the bound tree lifts the call, and lift it through `NullableLift`, which
  gains the equality's lift (an arrow called with the two operands, loose equality answering an absent
  one) and the relation's (`$eq.nullable.cmp`). The compound, the unary operator, the step, a tuple's
  element and a user-defined conversion in `ValueFlow` pass the lift they read. The emitted call of an
  operator that is not lifted is unchanged, byte for byte, and the transpiled components the runtime
  ships do not move.
- The public surface: the three `UserDefinedOperators` methods change their signatures, in
  `eQuantic.UI.Compiler`, a `tool` assembly whose surface no release promises. The developer surface
  does not move.
- The runtime, the realizers, the shells and the SDKs are untouched: the lift reuses `$eq.equals`,
  `$eq.nullable.arith`, `$eq.nullable.cmp` and `$eq.nullable.unary`.
- `tests/eQuantic.UI.Conformance.Tests/LiftedOperatorConformanceTests.cs`: the cases, executed on both
  sides, the spreadsheet's `SheetRange` through an app's module graph among them.
- Not reached by this change: a member named `Value` or `Count` of a nullable struct read through
  `?.` is taken for `Nullable<T>.Value` or a collection's count; `(Cents)n` from a null `int?` builds a
  value where .NET throws `InvalidOperationException`; `Equals` on a nullable struct, `Nullable.Equals`
  and `EqualityComparer<T?>.Default` fail the build with EQ2004.
