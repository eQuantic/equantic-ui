# Spec Delta

## ADDED Requirements

### Requirement: A member is compared by its type's default comparer

A record's and a struct's equality, and a value tuple's `Equals`, SHALL compare each member as
`EqualityComparer<T>.Default` compares its type: an array, a class and an interface no tuple
implements (a collection's, an app's) by reference or their own `Equals`, and a tuple holding an array
element by element with each element's own comparer. A tuple's `Equals(object)` SHALL find a tuple of
another arity unequal, and a null `Nullable` pair SHALL be equal only to another.

#### Scenario: An array member

- **WHEN** `new Items(new[] { 1 }) == new Items(new[] { 1 })` is computed for `record Items(int[] Values)`
- **THEN** it is false, as in .NET, where the arrays were compared element by element

#### Scenario: A tuple holding an array

- **WHEN** `(new[] { 1 }, 1).Equals((new[] { 1 }, 1))` is computed
- **THEN** it is false, as in .NET

#### Scenario: A collection behind its interface

- **WHEN** `new Viewed(new List<int> { 1 }) == new Viewed(new List<int> { 1 })` is computed for
  `record Viewed(IReadOnlyList<int> Values)`
- **THEN** it is false, as in .NET, where the lists were compared element by element

#### Scenario: A tuple of another arity

- **WHEN** `(a, 1).Equals((object)(a, 1, 2))` is computed
- **THEN** it is false, as in .NET, where the elements past the receiver's were not read

#### Scenario: A null pair

- **WHEN** `new Paired(null) == new Paired(null)` is computed for `record Paired(KeyValuePair<int, int[]>? Value)`
- **THEN** it is true, as in .NET, where reading the null pair threw
