## ADDED Requirements

### Requirement: A struct's operator over nullable values is lifted as C# lifts it

An operator applied over nullable values where C# lifts it, a record struct's equality, the equality of
a value the browser holds as data and an operator or a conversion a struct declares, SHALL answer as
C#'s lifted operator answers. An equality SHALL find two absent values equal and an absent value unequal
to any value, without the operator, and SHALL compare two values by the struct's own equality: a record
struct's member by member, a struct's by the `==` or `!=` it declares. A relation SHALL be false where a
value is absent. Any other operator, a unary one, a compound assignment and a step included, and a
user-defined conversion from a nullable value to a nullable value, SHALL answer null for an absent value,
the operator running only over a value. The operands SHALL be evaluated once each, in order. An operator
declared over the nullable values themselves SHALL be called as it is written, a null included, and an
element of a nullable type in a tuple's equality SHALL compare through the same lift.

#### Scenario: Two nullable record structs built apart

- **WHEN** browser-side code compares `Spot? a = new Spot(1, 2), b = new Spot(1, 2);` with `a == b` and
  `a != b`, for `record struct Spot(int Row, int Col)`, and keeps `var before = target;`, builds
  `target = new Spot(1, 2);` again and answers `target != before`
- **THEN** they are True, False and False, as in .NET, where the browser compared two references

#### Scenario: An absent record struct

- **WHEN** browser-side code compares `Spot? a = null, b = null;` and `Spot? c = new Spot(1, 2), d = null;`
  with `a == b` and `c == d`
- **THEN** they are True and False, as in .NET

#### Scenario: The spreadsheet's range

- **WHEN** a class of an app keeps a `SheetRange?`, rebuilds it for a row and answers whether it moved
  with `Target != before`, built as an app's build builds it, in TypeScript and in JavaScript
- **THEN** the same row is no move and another row is one, as in .NET, where every move was one

#### Scenario: A struct's own equality over nullable values

- **WHEN** `struct Reach` declares `==` and `!=` over `Start` alone, and browser-side code compares
  `Reach? a = new Reach(1, 5), b = new Reach(1, 9);`, `Reach? c = null, d = null;` and
  `Reach? e = new Reach(1, 5), f = null;` with `==`
- **THEN** they are True, True and False, as in .NET, the operator called for the two values only, where
  the browser handed it the null and threw

#### Scenario: A relation and the other operators of a struct

- **WHEN** browser-side code computes `a < b`, `a + b` and `-a` for `Reach? a = null, b = new Reach(2, 0);`,
  and `m += new Reach(2, 0)` for a null `Reach? m`
- **THEN** they are False, null, null and null, as in .NET

#### Scenario: A conversion between two nullable types

- **WHEN** `record struct Cents(int Amount)` declares `implicit operator Cents(int)` and
  `explicit operator int(Cents)`, and browser-side code computes `Cents? c = n;` and `(int?)c` for a null
  `int? n` and a null `Cents? c`
- **THEN** both are null, as in .NET, where the browser built a `Cents` holding null and threw

#### Scenario: An operator declared over the nullable values

- **WHEN** `struct Probe` declares `operator ==(Probe? a, Probe? b)`, which reads a null as -1, and
  browser-side code compares `Probe? a = null, b = new Probe(-1);` with `a == b`
- **THEN** it is True, the operator's own answer, as in .NET

#### Scenario: A tuple's nullable element

- **WHEN** browser-side code compares `(Reach?, int) a = (null, 1), b = (null, 1);` with `a == b`
- **THEN** it is True, as in .NET, where the browser handed `Reach`'s operator the nulls and threw
