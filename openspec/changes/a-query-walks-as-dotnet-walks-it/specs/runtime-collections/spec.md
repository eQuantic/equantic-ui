## ADDED Requirements

### Requirement: A walk over a List<T> ends when the list changes

A `List<T>` SHALL carry a version that every change .NET counts moves: `Add`, `Insert`, a `Remove`
that finds its item, `RemoveAt`, an `AddRange`, `InsertRange`, `RemoveRange` or `RemoveAll` that
changes something, `Clear`, `Sort`, `Reverse`, and an element written through the indexer, which
SHALL check its index. A `foreach` over the list, a query over it and `List.ForEach` SHALL compare it
at every step and after the last, and SHALL throw .NET's `InvalidOperationException` once it moved.

#### Scenario: A loop that adds to its list

- **WHEN** a `foreach` over a `List<int>` adds to the list in its body
- **THEN** the next step throws `InvalidOperationException` with "Collection was modified; enumeration operation may not execute."

#### Scenario: A change .NET does not count

- **WHEN** the body calls `l.Remove(42)` for an absent item, or `l.AddRange(new int[0])`
- **THEN** the loop runs to its end, as .NET's does

#### Scenario: An element written through the indexer

- **WHEN** the body writes `l[0] = l[0]`, the same value
- **THEN** the next step throws, as .NET's does, while a `T[]`'s element written during its walk ends nothing

#### Scenario: An index outside the list

- **WHEN** `l[1] = 5` runs over `new List<int> { 1 }`
- **THEN** it throws .NET's `ArgumentOutOfRangeException`, where the array grew

#### Scenario: A list held as an interface it answers to

- **WHEN** a `foreach` walks `IEnumerable<int> e = l` (or an `IList<int>`, an `IReadOnlyList<int>`) and its body adds to `l`
- **THEN** the next step throws `InvalidOperationException`, as .NET's list enumerator does through every face, while a set or an array held so walks as it is
