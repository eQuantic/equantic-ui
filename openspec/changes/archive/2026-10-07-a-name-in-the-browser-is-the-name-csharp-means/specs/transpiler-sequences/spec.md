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
