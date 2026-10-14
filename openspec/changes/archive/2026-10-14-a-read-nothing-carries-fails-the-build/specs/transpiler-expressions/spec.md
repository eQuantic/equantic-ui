## ADDED Requirements

### Requirement: A nullable tuple's element reads through the guard

An element of a nullable tuple read through `?.`, by its declared name or by its position, SHALL
answer the element, and null when the tuple is null, as in C#.

#### Scenario: By name and by position

- **WHEN** browser-side code computes `t?.A ?? -1` and `t?.Item2 ?? -1` for `(int A, int B)? t = (1, 2)`
- **THEN** they are 1 and 2, as in .NET; in the browser `t?.A` read undefined

#### Scenario: A null tuple

- **WHEN** browser-side code computes `t?.B ?? -1` for a null `(int A, int B)? t`
- **THEN** it is -1, as in .NET
