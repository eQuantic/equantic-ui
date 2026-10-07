# Spec Delta

## ADDED Requirements

### Requirement: A generic record equals only a value of its own closed type

A generic record's or struct's equality SHALL compare the closed type a value was built as, as .NET
compares a record's EqualityContract, wherever C# names the type arguments at the construction; `with`
SHALL keep the closed type of the value it copies. A value whose type arguments the build cannot know
(built inside generic code) SHALL NOT be taken for another type.

#### Scenario: One value under two type arguments

- **WHEN** `record Box<T>(T Value);` and `new Box<int>(1).Equals((object)new Box<double>(1))` runs
- **THEN** it answers false, as in .NET, and so does a `List<object>` holding the first asked to
  `Contains` the second, and `(new Box<int>(1) with { Value = 2 })` against `new Box<double>(2)`

#### Scenario: A generic struct

- **WHEN** `record struct Pair<T>(T A);` and `new Pair<int>(1).Equals((object)new Pair<double>(1))` runs
- **THEN** it answers false, as in .NET

#### Scenario: One closed type

- **WHEN** `new Box<int>(1)` is compared with `new Box<int>(1)`, and with a `Box<int>` built by
  `Make.Boxed(1)`, a generic method
- **THEN** both answer true, as in .NET
