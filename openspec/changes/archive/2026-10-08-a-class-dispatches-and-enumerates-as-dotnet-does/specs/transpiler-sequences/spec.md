## ADDED Requirements

### Requirement: A class that implements IEnumerable<T> is enumerated as its GetEnumerator() says

A class, a record or a struct that implements `IEnumerable<T>`, or only `IEnumerable`, SHALL be
enumerated in the browser by a `foreach`, a spread, `string.Join` and every LINQ operator as its own
`GetEnumerator()` says, each through the one C# binds for it: a `foreach` over the class through the
type's own public `GetEnumerator()` where it has one, and LINQ, a spread, `string.Join` and a `foreach`
over the interface through the interface's implementation, explicit or not, so that the two walk
different sequences where they answer differently. Through the enumerator an iterator method fills, or
one the app writes, a class that is its own enumerator included, it SHALL be walked by its `MoveNext`
and `Current` and disposed when the walk ends, however it ends. A derived type SHALL be enumerated
through its override of the method. The non-generic `IEnumerable.GetEnumerator()` beside the generic
interface's member, and an explicit `Current` beside a public one, SHALL NOT replace the member that
answers that name.

#### Scenario: A foreach over an iterator method

- **WHEN** `class Bag : IEnumerable<int>` adds `x * 10` to a list in its `Add`, yields the list in its
  `GetEnumerator()` and implements `IEnumerable.GetEnumerator()` explicitly, and
  `var bag = new Bag(); bag.Add(3); var t = 0; foreach (var x in bag) t += x; return t;`
- **THEN** it answers `30`, as .NET does, where it threw

#### Scenario: string.Join and LINQ

- **WHEN** `var bag = new Bag(); bag.Add(1); bag.Add(2); return string.Join(",", bag);` and
  `new Bag { 1, 2, 3 }.Where(x => x > 10).Sum()`
- **THEN** they answer `10,20` and `50`, as .NET does

#### Scenario: An enumerator the app wrote

- **WHEN** `class Countdown : IEnumerable<int>` returns a `CountdownEnumerator : IEnumerator<int>`
  that counts down and counts its `Dispose` calls, and a `foreach` over `new Countdown(5)` breaks at 4
- **THEN** the loop stops at 4 and the enumerator is disposed once, as .NET disposes it

#### Scenario: A public GetEnumerator that returns a struct

- **WHEN** `class Fast : IEnumerable<int>` declares `public Walk GetEnumerator() => new Walk(3);`, where
  `struct Walk : IEnumerator<int>` answers 2, 4 and 6, beside the explicit `IEnumerable<int>` and
  `IEnumerable` ones, and a `foreach`, `string.Join` and `Sum()` walk it
- **THEN** they answer 12, `2,4,6` and 12, as .NET does, where the explicit one replaced the public one,
  called itself and ran out of stack

#### Scenario: A public GetEnumerator and an explicit one that walk different sequences

- **WHEN** `class Split : IEnumerable<int>` yields 1 from its public `GetEnumerator()` and 2 from its
  explicit `IEnumerable<int>.GetEnumerator()`
- **THEN** a `foreach` over `new Split()` walks 1, and a `foreach` over it as `IEnumerable<int>`,
  `string.Join`, `Sum()`, a spread and `new List<int>(split)` walk 2, as .NET does, where every one of
  them walked the public one
