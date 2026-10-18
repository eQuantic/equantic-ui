## ADDED Requirements

### Requirement: A nullable value's Equals and the default comparer answer as the lifted equality does

`Equals` called on a nullable struct, `Nullable.Equals(a, b)` and `EqualityComparer<T>.Default.Equals(x, y)`
SHALL build and SHALL answer as .NET's: two empty values equal, an empty value unequal to any value, and two
values by their type's own equality, its `Equals(object)` for `Equals` and its `IEquatable<T>` before that
for the other two, never its `==`. The default comparer's `Equals` SHALL answer so over any type, a number,
a string and a record included. A struct's own equality that is the app's code SHALL be asked every time two
present values are compared, a value compared with itself included, as .NET asks it with no identity check,
and `Equals` on a value that is not nullable SHALL ask it with a null argument too. Both values SHALL be
evaluated once, in the order the call writes them. The comparer's `Default` read on its own SHALL still fail
the build with EQ2004, since no value the browser holds carries it, and only `EqualityComparer<T>`'s own
`Default` SHALL be taken for the default comparer: a call made on a static property of the app's named
`Default` SHALL still fail the build with EQ2004.

#### Scenario: Two nullable record structs

- **WHEN** `a.Equals(b)`, `Nullable.Equals(a, b)` and `EqualityComparer<Spot?>.Default.Equals(a, b)` run for
  `Spot? a = new Spot(1, 2), b = new Spot(1, 2)`, for two empty values, and for `Spot? a = new Spot(1, 2)`
  beside an empty `b`, where `record struct Spot(int Row, int Col)`
- **THEN** they answer True for the first two pairs and False for the third, as in .NET, where the build
  refused all three with EQ2004

#### Scenario: A struct's Equals is not its ==

- **WHEN** `struct Reach` declares `==` over `Start` alone, and `a.Equals(b)` and `Nullable.Equals(a, b)` run
  for `Reach? a = new Reach(1, 5), b = new Reach(1, 9)`
- **THEN** both answer False, as in .NET, where `a == b` answers True

#### Scenario: A struct's own Equals(object)

- **WHEN** `struct Loose` overrides `Equals(object)` to take another `Loose` by its last digit and an `int` by
  its value, and `a.Equals(b)`, `a.Equals((object)3)` and `Nullable.Equals(a, b)` run for
  `Loose? a = new Loose(3), b = new Loose(13)`
- **THEN** each answers True, as in .NET

#### Scenario: In the order written

- **WHEN** `Nullable.Equals(n2: At(2), n1: At(1))` runs, where `At` logs its argument and answers a `Spot?`
- **THEN** the log reads `21`, as in .NET

#### Scenario: The default comparer over any type

- **WHEN** `EqualityComparer<int>.Default.Equals(1, 1)`, `EqualityComparer<string>.Default.Equals("a", "b")`
  and `EqualityComparer<double>.Default.Equals(double.NaN, double.NaN)` run
- **THEN** they answer True, False and True, as in .NET

#### Scenario: The comparer read alone

- **WHEN** a method reads `EqualityComparer<Spot?>.Default` into a local
- **THEN** the build refuses it with EQ2004, naming `EqualityComparer<Spot?>.Default`

#### Scenario: A struct's own Equals asked every time

- **WHEN** `struct Counted` overrides `Equals(object)` to count its calls, and `a.Equals(a)`,
  `Nullable.Equals(a, a)` and `EqualityComparer<Counted?>.Default.Equals(a, a)` run for
  `Counted? a = new Counted(1)`, and `v.Equals(v)`, `v.Equals(null)` and
  `EqualityComparer<Counted>.Default.Equals(v, v)` for `Counted v = new Counted(1)`
- **THEN** each counts one call, as in .NET, where the browser answered the same value at its identity check
  and the null without asking; beside an empty nullable value the count stays 0

#### Scenario: An app's own Default property

- **WHEN** a static class of the app's declares `EqualityComparer<int> Default`, whose getter throws, and a
  method calls `Custom.Default.Equals(1, 1)`
- **THEN** the build refuses it with EQ2004, as it did before, where the call was lowered and the getter
  dropped unread
