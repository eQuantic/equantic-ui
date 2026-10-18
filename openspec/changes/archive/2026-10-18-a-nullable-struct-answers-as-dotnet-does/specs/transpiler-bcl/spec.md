## ADDED Requirements

### Requirement: A nullable value's Equals and the default comparer answer as the lifted equality does

`Equals` called on a nullable struct, `Nullable.Equals(a, b)` and `EqualityComparer<T>.Default.Equals(x, y)`
SHALL build and SHALL answer as .NET's: two empty values equal, an empty value unequal to any value, and two
values by their type's own equality, its `Equals(object)` for `Equals` and its `IEquatable<T>` before that
for the other two, never its `==`. The default comparer's `Equals` SHALL answer so over any type, a number,
a string and a record included. A value's own equality that is the app's code, a struct's or a class's
`Equals(object)`, declared or inherited from the app's base, and an `IEquatable<T>` its type answers, SHALL be
asked every time two present values are compared, a value compared with itself included, as .NET asks it with
no identity check, whether the call names the value's type, a base class the value's class extends, `object`,
an interface or a type parameter of generic code, and `Equals` on a struct value that is not nullable SHALL ask
it with a null argument too. A record's synthesized `Equals` SHALL answer a record compared with itself at the
reference check C# writes into it, before it compares a member, wherever the comparer reaches it. A class with
neither SHALL compare by reference. Both values SHALL be evaluated once, in the order the call writes
them. The comparer's `Default` read on its own SHALL still fail
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

#### Scenario: A class's own Equals asked by the default comparer

- **WHEN** `class Counter` overrides `Equals(object)` to count its calls, and `EqualityComparer<Counter>.Default.Equals(c, c)`
  runs for one `Counter c`, and so does the same call through `object`, an interface, a class that inherits the
  override and generic code, `EqualityComparer<T>.Default.Equals(a, b)` with `T` a `Counter`, built as an app's
  build builds it
- **THEN** each answers True and counts one call, as in .NET, and an override that refuses itself answers False,
  where the browser answered the same value at its identity check and counted none

#### Scenario: A class with no override

- **WHEN** a class declares neither `Equals(object)` nor `IEquatable<T>`, and
  `EqualityComparer<PlainClass>.Default.Equals(p, p)` and `EqualityComparer<PlainClass>.Default.Equals(p, new PlainClass(1))`
  run
- **THEN** they answer True and False, by reference, as in .NET

#### Scenario: A subclass's override behind its base

- **WHEN** `class Square : Shape` overrides `Equals(object)` to count its calls, where `Shape` overrides nothing,
  and `EqualityComparer<Shape>.Default.Equals(s, s)` runs for one `Shape s = new Square(1)`, and so do the same
  call behind an abstract base and `EqualityComparer<BaseRecord>.Default.Equals(e, e)` over a derived record whose
  own typed `Equals` counts its calls
- **THEN** each answers True and counts one call, as in .NET's virtual call, and a subclass that refuses itself
  answers False, where the browser answered the same value at its identity check and counted none

#### Scenario: A record's synthesized Equals checks the reference

- **WHEN** `record Wrapped(TypedClass T)` holds a member whose typed `Equals` counts its calls, and
  `EqualityComparer<Wrapped>.Default.Equals(w, w)` runs for one `Wrapped w`, and so does the same call through
  `object`, an interface the record implements and generic code
- **THEN** each answers True and counts no call, as C#'s synthesized `Equals` answers at its reference check,
  where through `object`, the interface and generic code the browser compared the record's member; two records
  holding one member count one call

#### Scenario: An app's own Default property

- **WHEN** a static class of the app's declares `EqualityComparer<int> Default`, whose getter throws, and a
  method calls `Custom.Default.Equals(1, 1)`
- **THEN** the build refuses it with EQ2004, as it did before, where the call was lowered and the getter
  dropped unread
