# Proposal

Closes #966 and #967 (Bugs) and #968 and #969 (Tasks), under #849 (The transpiler's fences hold on every
path, part three). They are what #948's lift (#972) measured and left: three answers a nullable struct
gave differently in the browser, and the sidestep the spreadsheet's engine took while #948 was open.

## Why

A nullable struct answered three things apart from .NET. A member a struct declares, read through `?.` on
a nullable value of it, was translated by its name (#966): `a?.Value` answered the struct itself and
`c?.Count` read a `length` no struct has. An empty nullable unwrapped to a value carried its null on
(#967): `(Cents)n` over a struct's `explicit operator Cents(int)` built a `Cents` holding null where
`Nullable<T>.Value` throws first, and so did a cast, a `foreach` element and `Value` itself. And `Equals`
over two nullable structs, `Nullable.Equals` and `EqualityComparer<Spot?>.Default.Equals` failed the build
with EQ2004 (#968), where .NET answers all three as #948's lifted `==` does.

Measured with 95 statement cases and 15 cases through an app's module graph, executed on both sides: 73 of
the first and 14 of the second, in TypeScript and in JavaScript alike, failed before the change.

## What Changes

For a developer, a nullable struct answers in the browser what it answers in .NET:

- `a?.Value`, `a?.HasValue`, `a?.GetValueOrDefault(x)` and `c?.Count` read the struct's own member,
  whatever it is called, behind `?.` and in a property pattern; `Nullable<T>`'s own members keep their
  meaning.
- `Value` on an empty nullable, a cast of one to its value's type or another, the conversion around the
  value a struct's own conversion operator takes, and a `foreach` element of a type that is not nullable
  throw `InvalidOperationException` ("Nullable object must have a value."), as .NET does. A present value
  converts as it did, and a lifted conversion still answers null for null.
- `Equals` over a nullable struct, `Nullable.Equals` and `EqualityComparer<T>.Default.Equals`, the last
  over any type, build and answer as .NET's: two empty values equal, an empty one unequal to any value, and
  two values by the type's own `Equals`, never its `==`. Reading `EqualityComparer<T>.Default` on its own is
  still refused, as is a primitive's nullable `Equals(object)`, which belongs with a primitive's own
  `Equals(object)` (#956, #957).
- The spreadsheet's engine writes its fill move as C# reads it, `return FillTarget != before;` (#969), and
  answers as it did.

Nothing breaks: an app that met the old answers met wrong ones, a null where .NET throws, or a build that
refused it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-expressions`: a nullable struct's own member is read as the struct's, and an empty nullable
  unwrapped to a value throws.
- `transpiler-bcl`: a nullable value's `Equals`, `Nullable.Equals` and the default comparer's `Equals`
  answer as the lifted equality does.
- `client-boundary`: a comparison that reaches a server-only `Equals` is refused where it is written, as a
  call bound to it is.

## Impact

- eqc: `NullableStrategy` asks the model which member a `Value`, `HasValue` or `GetValueOrDefault` binds and
  answers only `Nullable<T>`'s, and reads `Value` through the unwrap; `CountSpelling` reads a count on a
  nullable value's own type. `ValueFlow.Apply` writes the unwrap wherever the bound tree unwraps a nullable
  into a type that is not nullable (`ValueFlow.Unwraps`), before it converts the value, and
  `CastExpressionStrategy`'s enum branches unwrap their operand the same way. `StructuralEqualsStrategy`
  lowers `Nullable<T>.Equals(object)` over a struct as its value's call, and `Nullable.Equals<T>` and a call
  made on `EqualityComparer<T>.Default` as the comparison `ElementEquality` writes for `T`'s default comparer.
- The runtime gains `$eq.nullable.value`, which throws .NET's `InvalidOperationException` for an empty value.
  The transpiled modules the runtime ships move where they read a `Value` (`BarChartLayout`, `Calendar`,
  `SheetController`), and `SheetController` by its fill move's comparison; the served runtime grows by about
  550 gzipped bytes, inside its budget.
- The public surface: `Eq.NullableValue` is added to `eQuantic.UI.Compiler`, a `tool` assembly. The BCL audit
  records `Nullable<int>.Value` as routed through `$eq`. The developer surface does not move.
- The realizers, the shells and the SDKs are untouched.
- `tests/eQuantic.UI.Conformance.Tests/NullableStructConformanceTests.cs`: the cases, executed on both
  sides; `tests/eQuantic.UI.Compiler.Tests/Diagnostics/NullableEqualityTests.cs`: the build that refused the
  three calls and still refuses the comparer's `Default` read.
- Not reached by this change: a user-defined conversion nested with a standard one on the same node loses
  one of the two (#970), and a primitive's `Equals(object)`, nullable or not, answers a value of another
  type equal or fails the build (#956, #957).
