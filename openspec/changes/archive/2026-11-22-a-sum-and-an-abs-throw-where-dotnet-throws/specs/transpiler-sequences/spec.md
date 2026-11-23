## ADDED Requirements

### Requirement: Sum adds in the type the overload adds

`Enumerable.Sum` SHALL add in the type of the values the overload C# binds adds, the selector's result or the
source's element: an `int` and a `long` in a checked context whatever the context of the call, so the addition
that leaves the type's range throws .NET's `OverflowException` however the elements after it would bring the
total back, a `float` in a double rounded once, a `decimal` as a decimal, and a nullable over its values alone,
0 when it has none. A sum of reals SHALL be written as an inline reduce that adds with `+`, with nothing checked.

#### Scenario: A total past an int

- **WHEN** browser-side code computes `new[] { int.MaxValue, 1 }.Sum()`, or the same inside `unchecked`
- **THEN** it throws `OverflowException` with .NET's message, where the browser answered 2147483648

#### Scenario: A running total that leaves the range and comes back

- **WHEN** browser-side code computes `new[] { int.MaxValue, 1, -1 }.Sum()` or `new[] { long.MaxValue, 1L, -1L }.Sum()`
- **THEN** it throws `OverflowException`, as .NET does

#### Scenario: A selector that widens

- **WHEN** browser-side code computes `new[] { int.MaxValue, int.MaxValue, int.MaxValue }.Sum(x => (long)x)`
- **THEN** it is 6442450941, as in .NET

#### Scenario: A nullable with a null in it

- **WHEN** browser-side code computes `new long?[] { 1L, null, 2L }.Sum()` and `new decimal?[] { 1.5m, null, 2m }.Sum()`
- **THEN** they are 3 and 3.5, as in .NET, where the browser threw a TypeError
