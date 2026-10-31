## ADDED Requirements

### Requirement: A walk over a list ends when the list changes

A `foreach` over a `List<T>`, `List<T>.ForEach` and a LINQ query over a list SHALL compare the list's
version at every step, the step after the last element included, and SHALL end at the step after the
list changes with .NET's InvalidOperationException, "Collection was modified; enumeration operation may not
execute." A change SHALL be what .NET counts as one: `Add`, `Insert`, a `Remove` that finds the element,
`RemoveAt`, a range added or removed that holds anything, a `RemoveAll` that removes, `Clear`, `Sort` and
`Reverse` whatever they hold, and an element written through the indexer, the same value included. A read
SHALL be no change, and neither SHALL a `T[]`'s element written while the array is walked.

#### Scenario: A walk that adds as it goes

- **WHEN** browser-side code runs `foreach (var x in l) { s += x; if (l.Count > 9) break; l.Add(x); }` over
  `new List<int> { 1, 2 }`
- **THEN** it throws the InvalidOperationException after the first element, as .NET does; the browser walked
  `121212121`

#### Scenario: An element written through the indexer

- **WHEN** browser-side code writes `l[0] = l[0]` at the first step of a walk over a list
- **THEN** the walk throws at the next step, as .NET's does

#### Scenario: An index outside the list

- **WHEN** browser-side code writes `l[1] = 5` over `new List<int> { 1 }`
- **THEN** it throws .NET's ArgumentOutOfRangeException; the array grew instead
