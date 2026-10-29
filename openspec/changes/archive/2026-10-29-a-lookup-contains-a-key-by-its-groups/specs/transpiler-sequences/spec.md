## ADDED Requirements

### Requirement: A lookup's Contains asks for a group of that key

`ILookup<TKey, TElement>.Contains(key)` SHALL answer whether the lookup holds a group of that key, comparing keys
as the lookup's indexer and `ToLookup` compare them, by the key type's default comparer: a record, a decimal and a
date by value, a float or a double NaN equal to itself, a class by its own `Equals` where it overrides one, and a
null key a key like any other. The key SHALL be evaluated once, and a null lookup reached through `?.` SHALL answer
null. A LINQ `Contains` over a lookup's groups, `Enumerable.Contains(lookup, group)`, is outside this requirement:
it looks for a group, as it does over any sequence.

#### Scenario: A key of a string lookup

- **WHEN** browser-side code computes `l = new[] { "a", "b", "a" }.ToLookup(s => s)` and asks `l.Contains("b")` and `l.Contains("z")`
- **THEN** they answer `True` and `False`, as in .NET, where the browser answered `False` for both

#### Scenario: A record key

- **WHEN** browser-side code builds a lookup of `new Point(1, 2)` and `new Point(3, 4)`, a record, by the point itself, and asks it for `new Point(3, 4)` and `new Point(9, 9)`
- **THEN** they answer `True` and `False`, as in .NET: the equal record is found by value

#### Scenario: NaN and null keys

- **WHEN** browser-side code asks a lookup of `double.NaN` and `1.0` for `double.NaN`, and a lookup of `"a"` and `null` for `null`
- **THEN** both answer `True`, as in .NET

#### Scenario: The key runs once

- **WHEN** browser-side code asks a lookup of three groups `l.Contains(K())`, `K` counting its calls and answering the last group's key
- **THEN** it answers `True` and `K` ran once, as in .NET
