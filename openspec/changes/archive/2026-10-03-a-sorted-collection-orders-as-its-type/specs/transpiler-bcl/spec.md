## ADDED Requirements

### Requirement: A sorted collection orders as its element type does

A `SortedSet`, a `SortedDictionary` and a `SortedList` built in the browser SHALL order their
elements or keys as .NET's `Comparer<T>.Default` orders the element type: a number by its value, a
double with its NaN first and equal to another NaN, a string in the current culture, and a decimal, a
date and a type of the app's own that implements `IComparable` by its `CompareTo`. A
`StringComparer.Ordinal` handed to a sorted set SHALL order by code unit.

#### Scenario: Strings in the culture

- **WHEN** browser-side code adds `b`, `B`, `a`, `A` and `_x` to a `SortedSet<string>` and enumerates it
- **THEN** it enumerates `_x`, `a`, `A`, `b`, `B`, as in .NET

#### Scenario: Decimals by their value

- **WHEN** browser-side code adds `10m`, `9m`, `1.0m` and `1.00m` to a `SortedSet<decimal>`
- **THEN** it holds three elements and enumerates `1.0`, `9`, `10`, as in .NET

#### Scenario: An ordinal comparer

- **WHEN** browser-side code builds `new SortedSet<string>(StringComparer.Ordinal)` and adds `b`, `B`,
  `a` and `A`
- **THEN** it enumerates `A`, `B`, `a`, `b`, as in .NET
