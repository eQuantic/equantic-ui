## ADDED Requirements

### Requirement: A LINQ operator takes a comparer the collection fence passes

A comparer handed to `ToDictionary`, `ToLookup`, `GroupBy`, `Distinct` or `ToHashSet` SHALL pass the
fence a collection's construction passes: one that asks for the key type's default equality
(`EqualityComparer<T>.Default`, a null comparer, or `StringComparer.Ordinal` for a string) SHALL answer
as the operator does without it, its selectors in their parameters whether named or not, and any other
SHALL fail the build with EQ2007, never be dropped or called as a selector.

#### Scenario: An ordinal dictionary

- **WHEN** browser-side code computes `new[] { "a", "bb" }.ToDictionary(w => w, StringComparer.Ordinal)` and reads its `"bb"` entry and its count
- **THEN** the answer is `bb|2`, as in .NET

#### Scenario: A lookup with a comparer

- **WHEN** browser-side code computes `new[] { "a", "bb", "cc" }.ToLookup(w => w.Length, EqualityComparer<int>.Default)` and joins its group of 2
- **THEN** the answer is `bb,cc`, as in .NET

#### Scenario: A comparer that changes equality

- **WHEN** a component computes `words.Distinct(StringComparer.OrdinalIgnoreCase)`
- **THEN** the build fails with EQ2007 at the comparer
