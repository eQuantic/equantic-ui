# Spec Delta

## ADDED Requirements

### Requirement: A member is compared by its type's default comparer

A record's and a struct's equality, and a value tuple's `Equals`, SHALL compare each member as
`EqualityComparer<T>.Default` compares its type: an array and a class by reference or their own
`Equals`, and a tuple holding an array element by element with each element's own comparer.

#### Scenario: An array member

- **WHEN** `new Items(new[] { 1 }) == new Items(new[] { 1 })` is computed for `record Items(int[] Values)`
- **THEN** it is false, as in .NET, where the arrays were compared element by element

#### Scenario: A tuple holding an array

- **WHEN** `(new[] { 1 }, 1).Equals((new[] { 1 }, 1))` is computed
- **THEN** it is false, as in .NET
