## ADDED Requirements

### Requirement: An enum is held and combined at its underlying type's width

A value of an enum SHALL be held at its underlying type's width: a number for a type of 32 bits or
fewer, never negative for an unsigned one, and a BigInt for a `long` or a `ulong`, as a `long` is held.
A cast into or out of an enum SHALL convert as the numeric conversion between the underlying types
converts, throwing where .NET throws in a checked context. The operators C# defines on an enum (`|`,
`&`, `^`, `~`, `+`, `-`, `++`, `--`, their compound assignments, the relations and a relational
pattern), `HasFlag` and `CompareTo` SHALL compute on the values at that width, a lifted one answering
null for an absent operand. A 64-bit enum SHALL cross between the server and the browser as a `long`
does, as a value and as a dictionary key.

#### Scenario: A member past 2^53

- **WHEN** browser-side code reads `(long)Huge.Top` and `Huge.Top.ToString("X")` of
  `[Flags] enum Huge : long { None = 0, A = 1, B = 1L << 40, Top = 1L << 62 }`
- **THEN** they are 4611686018427387904 and `4000000000000000`, as in .NET

#### Scenario: Flags past 32 bits

- **WHEN** browser-side code reads `(long)(a | b)` for `var a = Huge.A; var b = Huge.B;`
- **THEN** it is 1099511627777, as in .NET, where it was 1

#### Scenario: A uint flag's high bit

- **WHEN** browser-side code reads `(a | t).HasFlag(t)` for `var a = UFlags.A; var t = UFlags.Top;` of
  `[Flags] enum UFlags : uint { None = 0, A = 1, Top = 0x80000000 }`
- **THEN** it is true, as in .NET, where it was false

#### Scenario: A checked cast

- **WHEN** browser-side code runs `checked((int)t)` for `var t = Huge.Top;`
- **THEN** it throws an `OverflowException`, as in .NET, and `unchecked((int)t)` is 0

#### Scenario: Crossing from the server

- **WHEN** the server renders a page whose field holds `CrossingReach.Far | CrossingReach.Near` of
  `[Flags] enum CrossingReach : long { None = 0, Near = 1, Far = 1L << 62 }`, and a dictionary keyed by it
- **THEN** the browser's first build draws `4611686018427387905 Near, Far` and finds the dictionary's
  keys, as the server drew them
