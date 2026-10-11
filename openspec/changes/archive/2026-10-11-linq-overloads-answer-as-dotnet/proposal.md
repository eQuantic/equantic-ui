# Proposal

Closes #719, #720, #721, #722 and #727, Bugs under #565 (The transpiler's fences hold on every path,
continued). The first four were found by #715's null-argument measurement, which listed each overload
as a gap because eqc lowered it as a sibling it was not; the fifth by its strict `tsc` over a shared
component.

## Why

Executed on both sides at a7d5f3d, where 37 of the 53 cases of `LinqOverloadConformanceTests` fail:

- `new int[0].FirstOrDefault(9)` and `LastOrDefault(9)` threw a `TypeError` in the browser: the default
  value was called as the predicate (`[].find(9)`). With a predicate, `FirstOrDefault(x => x > 5, 9)`
  answered 0, the element type's default, where .NET answers 9 (#720).
- `a.Zip(b, c)`, .NET 6's triple overload, was lowered as the result-selector overload, which called the
  third sequence as a function, and `a.Zip(b)` answered its receiver's elements as the pairs (#722).
- A mean of longs through a method group or a delegate, `Average(Widen)`, threw `Cannot mix BigInt and
  other types`: the reduce read the type from a lambda's body, and seeded a number. `new long?[] { 1, null,
  4 }.Average()` threw the same way, `new double?[] { 1.5, null }.Average()` answered 0.75 by dividing by
  the null too, an empty sequence answered `NaN` where .NET throws, and an overflow past `long.MaxValue`
  was not refused (#719).
- `a.SequenceEqual(b, EqualityComparer<int>.Default)` answered its first sequence, truthy whatever `b`
  held, and a comparer that changes equality was dropped rather than refused. Without one, the JSON text
  of each sequence was compared: two arrays of longs threw, `1.0m` was not `1.00m`, a null was a `NaN`,
  and two arrays of the same items were one (#721).
- In a type-checked module, `GroupBy` and `ToLookup` failed `tsc`: the grouping's accumulator was typed
  by its empty seed, `never[]` over a typed source and `any` over a loose one (#727).

## What Changes

- The runtime gains five twins in `$eq.linq`, each the .NET overload's own algorithm:
  `firstOrDefault` and `lastOrDefault` with a default value, `zipTuples` for pairs and triples,
  `average` by the mean's type (`'number'`, `'single'`, `'long'`, `'decimal'`, nullable or not), and
  `sequenceEqual` by the element type's default equality (`KeyEquality`, the one `Distinct` and the
  sets already use). Each refuses a null argument by name, as .NET does.
- eqc hands each call to its twin from the BOUND overload: a default value is recognised by the
  parameter it binds to, not by its position, so a named argument written out of order fills its own
  place and still runs where it stands. `LinqArguments` is the one place that builds such a call.
- `SequenceEqual`'s comparer passes the collection fence: one that asks for the default is dropped, any
  other fails the build with EQ2007.
- `GroupBy` and `ToLookup` type their accumulator in a type-checked module.

For a developer: each of these overloads answers in the browser what it answers in .NET, and a
`SequenceEqual` with a comparer that changes equality is a build error instead of a wrong answer.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `transpiler-sequences`: a LINQ overload answers as its own .NET overload, `SequenceEqual` takes a
  comparer the fence passes, and a grouping in a type-checked module passes `tsc`.

## Impact

- eqc: `First`, `Last`, `Zip`, `Average`, `SequenceEqual`, `GroupBy` and the lookup strategies, the new
  `LinqArguments`, and five `Eq` constants (public surface: `PublicAPI.Unshipped.txt`).
- Runtime: `utils/linq.ts` and the `$eq.linq` namespace. The served runtime grows by the five twins.
- Not reached: the web realizer, Photon, the SDKs and the templates. The developer surface does not move.
- #715's null-argument baseline shrinks by 33 entries and the BCL audit moves eight verdicts to `eq`.
- Left for an issue of its own: a lambda parameter of type `long` or `decimal` is annotated `number` in a
  type-checked module (#758).
