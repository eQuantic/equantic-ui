## ADDED Requirements

### Requirement: A native integer is held as a 64-bit integer

An `nint` and an `nuint` SHALL be held as a `long` and a `ulong` are, whatever value they came from, an
`int`'s, a `long`'s or a constant's, and their arithmetic, comparisons, conversions, `MinValue`, `MaxValue`
and `Math` members SHALL answer at 64 bits, as .NET answers on every target this SDK ships to.

#### Scenario: An int and a long meeting

- **WHEN** `nint a = (nint)9007199254740993; int i = 5; nint b = i; return (a + b).ToString();` runs
- **THEN** it answers 9007199254740998, as .NET does; it threw a TypeError for mixing a number with a BigInt

### Requirement: An implicit conversion is applied whole

Every implicit conversion the bound tree wraps a value in SHALL be applied, inner first: a user-defined
conversion with the standard conversion before or after its operator, and a tuple into a tuple of other
element types, each element converted as C# converts it and the source read once, at an assignment, an
argument, a return and a branch alike.

#### Scenario: An operator then a widening

- **WHEN** `long l = new Count(5); return (l * 2).ToString();` runs, `Count` declaring an implicit operator to
  an `int`
- **THEN** it answers 10, as .NET does; it threw a TypeError

#### Scenario: A widening then an operator

- **WHEN** `Ticks t = 5;` runs, `Ticks` declaring an implicit operator from a `long`
- **THEN** `t` is a `Ticks`, as in .NET; it was the BigInt 5

#### Scenario: A tuple of wider elements

- **WHEN** `(int, int) u = (1, 2); (long, long) t = u; return (t.Item1 + 1L).ToString();` runs
- **THEN** it answers 2, as .NET does, and `t.Equals((object)(1L, 2L))` answers True
