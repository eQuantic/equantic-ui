## ADDED Requirements

### Requirement: A range over a type with a Slice calls its Slice

A range used as an index over a type eqc writes, with a `Length` or a `Count` and a
`Slice(int start, int length)`, SHALL call that `Slice` as C# lowers the range: with the start and the
length computed from the endpoints and the count, the receiver evaluated once, then the endpoints in
the order they are written, then the count, read only where an endpoint counts from the end or the end
is left open. A range handed to an indexer that takes the `Range` itself SHALL fail the build with
EQ2004, since a System.Range value has no translation. A range over a string, an array or a list SHALL
keep answering as it does.

#### Scenario: A range over a type with Slice

- **WHEN** browser-side code computes `new Strip()[1..3]` over a type whose `Slice(start, length)` copies five elements
- **THEN** the slice holds 2 elements, `2,3`, as in .NET, where `slice(1, 3)` held three

#### Scenario: The order a range is read in

- **WHEN** browser-side code computes `Strip.R(s)[^Strip.At("A", 3)..^Strip.At("B", 1)]`, each call logging its step and `Length` logging `L`
- **THEN** the log is `RABL` before the slice, as in .NET

#### Scenario: An indexer over Range

- **WHEN** a component computes `new Ranged()[1..3]` over a type that declares `this[Range r]`
- **THEN** the build fails with EQ2004 at the range
