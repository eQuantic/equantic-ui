## ADDED Requirements

### Requirement: A LINQ argument is evaluated once, in C#'s order

The source of a LINQ call and each of its arguments SHALL be evaluated once, in the order C# evaluates them, whatever the lowering does with them per element. A lambda written in place MAY be made again per element, since nothing can tell.

#### Scenario: A second sequence that is a call

- **WHEN** `new[] { 1, 2, 3, 4 }.Intersect(Other())` runs, where `Other` counts its calls
- **THEN** `Other` has run once, as in .NET, and so it has for `Except`

#### Scenario: A source named twice

- **WHEN** `Items().Average()` runs, where `Items` counts its calls
- **THEN** `Items` has run once, as in .NET

#### Scenario: A selector that is a call

- **WHEN** `new[] { 1, 2, 3 }.Sum(Make())` or `GroupBy(Key())` runs, where the factory counts its calls
- **THEN** the factory has run once, as in .NET

#### Scenario: A selector held in a variable

- **WHEN** `new[] { 1, 2, 3 }.Sum(selector)` runs, where `selector` reassigns its own variable on its
  first call
- **THEN** every element goes through the delegate passed, and the sum is 6, as in .NET

#### Scenario: A second sequence with a lowering of its own

- **WHEN** `new[] { 1, 2, 3 }.Intersect(Other().DistinctBy(x => x))` runs, where `Other` counts its calls
- **THEN** `Other` has run once, as in .NET, and so it has for `Except`

#### Scenario: Eleven ordering keys

- **WHEN** an `OrderBy` is followed by ten `ThenBy`
- **THEN** the module parses, and the order is .NET's

#### Scenario: A source a selector reassigns

- **WHEN** `xs.Average(x => { xs = new[] { 1 }; return x; })` runs over `{ 1, 2, 3, 4 }`
- **THEN** it answers 2.5, as in .NET, which read `xs` once

#### Scenario: A string that quotes a lowering's own name

- **WHEN** `new[] { 1, 2, 3, 4 }.Intersect(Other("$x"))` runs, where `Other` counts its calls
- **THEN** `Other` has run once, as in .NET, and so it has when the string is interpolated

#### Scenario: A source bound once keeps its type

- **WHEN** a shared component computes `Items().Average()`, where `Items()` answers a `List<int>`
- **THEN** the module eqc writes passes the runtime's strict tsc: the source is bound bare, typed by
  the list, so the callback the lowering hands it is typed too
