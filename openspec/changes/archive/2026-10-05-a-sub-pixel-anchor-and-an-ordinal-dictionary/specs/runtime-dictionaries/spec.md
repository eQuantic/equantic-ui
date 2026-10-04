# Spec Delta

## ADDED Requirements

### Requirement: A comparer that asks for the default builds the dictionary

A `Dictionary`, a `SortedDictionary` or a `SortedList` constructed with a comparer that asks for
nothing the browser does not already do (`null`, `EqualityComparer<T>.Default`,
`Comparer<T>.Default`, or `StringComparer.Ordinal`) SHALL build, and SHALL answer as .NET's does:
its entries, its lookups, a copy of the dictionary or pairs it was handed, and, for a sorted one, the
order its comparer asks for, which is the code-unit order for `StringComparer.Ordinal` and the key
type's own for the default. A comparer that changes equality or order beyond that SHALL fail the
build once, with EQ2007, and never become the dictionary's seed.

#### Scenario: An ordinal dictionary with an indexer initializer

- **WHEN** a component builds `Dictionary<string, int> d = new(StringComparer.Ordinal) { ["b"] = 1, ["a"] = 2 }`
- **THEN** the build succeeds, the keys enumerate `b, a`, `d["a"]` is 2 and `d.ContainsKey("A")` is false

#### Scenario: A sorted dictionary with the ordinal comparer

- **WHEN** a component builds a `SortedDictionary<string, int>` with `StringComparer.Ordinal` and the
  keys `b`, `B` and `a`
- **THEN** the keys enumerate `B, a, b`

#### Scenario: A comparer that changes equality

- **WHEN** a component builds `new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)`
- **THEN** the build fails with EQ2007, and with no other error for the same construction
